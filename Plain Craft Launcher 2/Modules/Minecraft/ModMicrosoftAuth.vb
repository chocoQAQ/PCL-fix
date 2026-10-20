'微软公共客户端登录：不使用 Client Secret，不记录或上传登录令牌。
Public Module ModMicrosoftAuth
    Public Const MsOAuthBaseUrl As String = "https://login.microsoftonline.com/consumers/oauth2/v2.0/"
    Private Const MsOAuthScope As String = "XboxLive.signin offline_access"

    Public Function MsGetClientId() As String
        Dim Id As Guid
        If Not Guid.TryParse(OAuthClientId, Id) OrElse Id = Guid.Empty Then
            Throw New Exception("$微软应用编号无效。请检查 PCL_MS_CLIENT_ID 环境变量，或删除它以使用修复版自带的应用编号。")
        End If
        Return Id.ToString()
    End Function

    Private Function MsOAuthPost(Path As String, Body As String, Optional Timeout As Integer = 25000) As JObject
        Return NetRequestByClient(MsOAuthBaseUrl & Path, HttpMethod.Post, Content:=Body,
            ContentType:="application/x-www-form-urlencoded", Timeout:=Timeout,
            RequireJson:=True, MakeLog:=False).DeserializeJson()
    End Function

    Public Function MsRequestDeviceCode() As JObject
        Try
            Dim Result = MsOAuthPost("devicecode", $"client_id={MsGetClientId()}&scope={StringUtils.FormUrlEscape(MsOAuthScope)}")
            Result("pcl_expires_at") = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Result("expires_in").ToObject(Of Integer)()
            Return Result
        Catch ex As HttpRequestCodeException
            Throw MsOAuthFailure(ex.Response)
        End Try
    End Function

    Public Function MsPollDeviceCode(Data As JObject, IsCanceled As Func(Of Boolean)) As JObject
        Dim Timer = Stopwatch.StartNew()
        Return MsPollDeviceCodeCore(Data, IsCanceled,
            Function(Body) MsOAuthPost("token", Body, 15000),
            Sub(Milliseconds) Thread.Sleep(Milliseconds),
            Function() Timer.Elapsed.TotalSeconds)
    End Function

    '分离时钟和请求，以便回归验证待确认、限流、超时、取消等协议分支。
    Friend Function MsPollDeviceCodeCore(Data As JObject, IsCanceled As Func(Of Boolean),
            Request As Func(Of String, JObject), Wait As Action(Of Integer), ElapsedSeconds As Func(Of Double)) As JObject
        Dim Interval = Math.Max(1, If(Data("interval") Is Nothing, 5, Data("interval").ToObject(Of Integer)()))
        Dim ExpiresIn As Double = Data("expires_in").ToObject(Of Integer)()
        If Data("pcl_expires_at") IsNot Nothing Then
            ExpiresIn = Math.Min(ExpiresIn, Data("pcl_expires_at").ToObject(Of Long)() - DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        End If
        Dim Body = $"grant_type=urn:ietf:params:oauth:grant-type:device_code&client_id={MsGetClientId()}&device_code={StringUtils.FormUrlEscape(Data("device_code").ToString())}"
        Dim Failures As Integer = 0
        Do
            '所有轮询（包括网络重试）至少等待服务器给出的 interval，且可随时取消。
            Dim NextPoll = ElapsedSeconds() + Interval
            Do While ElapsedSeconds() < NextPoll
                If IsCanceled() Then Throw New OperationCanceledException()
                If ElapsedSeconds() >= ExpiresIn Then Throw MsOAuthFailure("{""error"":""expired_token""}")
                Wait(100)
            Loop
            If IsCanceled() Then Throw New OperationCanceledException()
            If ElapsedSeconds() >= ExpiresIn Then Throw MsOAuthFailure("{""error"":""expired_token""}")
            Try
                Dim Result = Request(Body)
                MsValidateTokens(Result)
                Return Result
            Catch ex As HttpRequestCodeException
                Select Case MsOAuthErrorCode(ex.Response)
                    Case "authorization_pending"
                        Failures = 0
                        Continue Do
                    Case "slow_down"
                        Interval += 5
                        Continue Do
                End Select
                If CInt(ex.StatusCode) = 429 Then
                    Interval += 5
                ElseIf CInt(ex.StatusCode) < 500 Then
                    Throw MsOAuthFailure(ex.Response)
                End If
                Failures += 1
                If Failures > 3 Then Throw New Exception("$微软登录服务暂时无法响应，请稍后重试。")
            Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is WebException OrElse TypeOf ex Is TimeoutException
                Failures += 1
                If Failures > 3 Then Throw New Exception("$连接微软登录服务失败，请检查网络后重试。")
            End Try
        Loop
    End Function

    Public Function MsRefreshOAuthToken(RefreshToken As String) As (OAuthAccessToken As String, OAuthRefreshToken As String)
        Try
            '刷新令牌只发送一次，避免并行请求造成轮换令牌相互覆盖。
            Dim Result = MsOAuthPost("token", $"client_id={MsGetClientId()}&refresh_token={StringUtils.FormUrlEscape(RefreshToken)}&grant_type=refresh_token&scope={StringUtils.FormUrlEscape(MsOAuthScope)}")
            MsValidateTokens(Result)
            Return (Result("access_token").ToString(), Result("refresh_token").ToString())
        Catch ex As HttpRequestCodeException
            If MsOAuthErrorCode(ex.Response) = "invalid_grant" OrElse
                If(ex.Response, "").ContainsIgnoreCase("expired") OrElse
                If(ex.Response, "").ContainsIgnoreCase("must sign in again") Then
                Return ("Relogin", "")
            End If
            Throw MsOAuthFailure(ex.Response)
        End Try
    End Function

    Private Sub MsValidateTokens(Result As JObject)
        If String.IsNullOrWhiteSpace(CStr(Result("access_token"))) OrElse String.IsNullOrWhiteSpace(CStr(Result("refresh_token"))) Then
            Throw New Exception("$微软返回的登录凭据不完整，请重新登录。")
        End If
    End Sub

    Friend Function MsOAuthErrorCode(Response As String) As String
        Try
            Return If(CStr(JObject.Parse(Response)("error")), "")
        Catch
            Return ""
        End Try
    End Function

    Friend Function MsOAuthFailure(Response As String) As Exception
        Response = If(Response, "")
        Dim Code = MsOAuthErrorCode(Response)
        If Response.Contains("AADSTS7000218") Then
            Return New Exception("$微软应用尚未启用公共客户端登录。请在 Entra 应用注册的身份验证设置中允许公共客户端流；桌面启动器无需 Client Secret。")
        ElseIf Code = "invalid_client" OrElse Code = "unauthorized_client" OrElse Response.Contains("AADSTS700016") Then
            Return New Exception("$微软应用注册配置不正确。请检查 Client ID、个人 Microsoft 账户支持和公共客户端流设置。")
        ElseIf Code = "invalid_scope" Then
            Return New Exception("$微软应用未接受 XboxLive.signin / offline_access 权限，请检查应用注册配置。")
        ElseIf Code = "authorization_declined" OrElse Code = "access_denied" Then
            Return New Exception("$你取消了微软登录或拒绝了应用申请的权限。")
        ElseIf Code = "expired_token" Then
            Return New Exception("$登录代码已过期，请重新登录。")
        ElseIf Code = "bad_verification_code" Then
            Return New Exception("$微软未识别此登录代码，请重新登录。")
        ElseIf Response.Contains("Account security interrupt") Then
            Return New Exception("$该账号因安全问题无法登录，请前往微软账户页检查。")
        ElseIf Response.Contains("service abuse") Then
            Return New Exception("$该账号已被微软封禁，无法登录。")
        ElseIf Response.Contains("AADSTS70000") Then
            Return New RestartException()
        End If
        '只显示协议错误码，不把包含身份信息的完整响应写进提示或日志。
        If Not System.Text.RegularExpressions.Regex.IsMatch(Code, "^[a-zA-Z0-9_.-]{1,80}$") Then Code = "unknown_error"
        Return New Exception($"$微软登录失败（{Code}），请稍后重试或检查应用注册配置。")
    End Function

    Friend Function MsMinecraftLoginFailure(StatusCode As Integer, Response As String) As Exception
        Response = If(Response, "")
        If Response.Contains("ACCOUNT_SUSPENDED") Then
            Return New Exception("$该账号似乎已被微软封禁，无法登录。")
        ElseIf Response.IndexOf("Invalid app registration", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return New Exception("$Minecraft 服务尚未授权此应用编号。请确认此 Client ID 的 Minecraft API 审核已获批；仅注册微软应用或提交审核申请并不代表获批。")
        ElseIf StatusCode = 429 Then
            Return New Exception("$登录尝试太过频繁，请等待几分钟后再试。")
        ElseIf StatusCode = 403 Then
            Return New Exception("$Minecraft 服务拒绝了登录。请先确认此 Client ID 的 Minecraft API 审核状态；若已获批，再检查账户或网络限制。")
        ElseIf StatusCode = 503 Then
            Return New Exception("$Minecraft 登录服务暂时不可用，请稍后重试。")
        End If
        Return New Exception($"$Minecraft 登录失败（HTTP {StatusCode}），请稍后重试。")
    End Function
End Module
