'MIT License

'Copyright (c) 2021 HBSnail

'Permission is hereby granted, free of charge, to any person obtaining a copy
'of this software and associated documentation files (the "Software"), to deal
'in the Software without restriction, including without limitation the rights
'to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
'copies of the Software, and to permit persons to whom the Software is
'furnished to do so, subject to the following conditions:

'The above copyright notice and this permission notice shall be included in all
'copies or substantial portions of the Software.

'THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
'IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
'FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
'AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
'LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
'OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
'SOFTWARE.

Imports System.IO
Imports System.Net
Imports System.Net.Security
Imports System.Net.Sockets
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports System.Threading

Friend Class HTTPSConnection
    Private BaseServiceProvider As FakeLocationServer
    Private RemEndPoint As IPEndPoint
    Private iClientBuffer As Byte()
    Private iRemoteBuffer As Byte()
    Private Client As Socket
    Private Key As String
    Public HostName As String
    Public Port As Integer
    Private ClientSSLStream As SslStream
    Private ServerSSLStream As SslStream
    Private store As New X509Store
    Private Server As Socket
    Public CTS As Boolean = True

    Public Sub New(FakeLocationServiceHandle As FakeLocationServer, client As Socket, key As String)
        Me.BaseServiceProvider = FakeLocationServiceHandle
        Me.Client = client
        Me.Key = key
    End Sub

    Public Sub Open()
        Try
            AlsHead(Client)
            RemEndPoint = New IPEndPoint(Dns.GetHostAddresses(HostName)(0), Port)
            CreateBridge()
            Return
        Catch ex As Exception
            Close(ex.Message)
            Return
        End Try
    End Sub

    Private Sub CreateBridge()
        Try

            Server = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            Dim t As New Thread(AddressOf connectimeoutwatcher)
            t.Start()
            Server.Connect(RemEndPoint)

            Me.Client.ReceiveBufferSize = 65535
            Me.Server.ReceiveBufferSize = 65535
            iClientBuffer = New Byte(Me.Client.ReceiveBufferSize - 1) {}
            iRemoteBuffer = New Byte(Me.Server.ReceiveBufferSize - 1) {}
            Me.Client.Send(Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established" + vbCrLf + vbCrLf))

            Me.Client.ReceiveTimeout = 10000
            Me.Client.SendTimeout = 10000
            Me.Server.ReceiveTimeout = 10000
            Me.Server.SendTimeout = 10000

            If (HostName Like "*.amap.com") Then
                Me.ServerSSLStream = New SslStream(New NetworkStream(Server), False, New RemoteCertificateValidationCallback(AddressOf ValidateServerCertificate))
                Me.ClientSSLStream = New SslStream(New NetworkStream(Client), False)
                Try

                    Me.ServerSSLStream.AuthenticateAsClient(HostName, store.Certificates(), 16368, False)
                Catch ex As Exception
                    Close(ex.Message)
                    Return
                End Try

                Try
                    Me.ClientSSLStream.AuthenticateAsServer(BaseServiceProvider.globalCert, False, 16368, False)
                Catch ex As Exception
                End Try
                Me.ClientSSLStream.ReadTimeout = 10000
                Me.ServerSSLStream.ReadTimeout = 10000
                Me.ClientSSLStream.WriteTimeout = 10000
                Me.ServerSSLStream.WriteTimeout = 10000

                Me.ClientSSLStream.BeginRead(iClientBuffer, 0, iClientBuffer.Length, New AsyncCallback(AddressOf Me.OnClientSSLReceive), ClientSSLStream)
                Me.ServerSSLStream.BeginRead(iRemoteBuffer, 0, iRemoteBuffer.Length, New AsyncCallback(AddressOf Me.OnRemoteSSLReceive), ServerSSLStream)
            Else
                Me.Client.BeginReceive(iClientBuffer, 0, iClientBuffer.Length, 0, New AsyncCallback(AddressOf Me.OnClientReceive), Me.Client)
                Me.Server.BeginReceive(iRemoteBuffer, 0, iRemoteBuffer.Length, 0, New AsyncCallback(AddressOf Me.OnRemoteReceive), Me.Server)

            End If
        Catch ex As Exception
            Close(ex.Message)
            Return
        End Try
    End Sub

    Private Sub OnRemoteReceive(ar As IAsyncResult)
        Try
            If (Me.Server IsNot Nothing) AndAlso (Me.Client IsNot Nothing) AndAlso Me.Server.Connected AndAlso Me.Client.Connected Then
                Dim s As Socket = TryCast(ar.AsyncState, Socket)
                If s.Connected Then
                    Dim size As Integer = s.EndReceive(ar)
                    If size > 0 Then
                        Dim SendBuffer As Byte() = New Byte(size - 1) {}
                        Array.Copy(iRemoteBuffer, SendBuffer, size)
                        If Client.Connected Then
                            Me.Client.Send(SendBuffer)
                        End If
                        If BaseServiceProvider.Working = True Then Me.Server.BeginReceive(iRemoteBuffer, 0, iRemoteBuffer.Length, 0, New AsyncCallback(AddressOf Me.OnRemoteReceive), Me.Server)
                    Else
                        Close("No more data to receive,this connection will be closed.(S/C)")
                        Return
                    End If
                End If
            Else
                Close("Connection closed.(S/C)")
                Return
            End If
        Catch ex As Exception
            Close(ex.Message)
            Return
        End Try
    End Sub

    Private Sub OnClientReceive(ar As IAsyncResult)
        Try
            If (Me.Server IsNot Nothing) AndAlso (Me.Client IsNot Nothing) AndAlso Me.Server.Connected AndAlso Me.Client.Connected Then
                Dim s As Socket = TryCast(ar.AsyncState, Socket)
                If s.Connected Then
                    Dim size As Integer = s.EndReceive(ar)
                    If size > 0 Then
                        Dim SendBuffer As Byte() = New Byte(size - 1) {}
                        Array.Copy(iClientBuffer, SendBuffer, size)
                        If Server.Connected Then
                            Me.Server.Send(SendBuffer)
                        End If
                        If BaseServiceProvider.Working = True Then Me.Client.BeginReceive(iClientBuffer, 0, iClientBuffer.Length, 0, New AsyncCallback(AddressOf Me.OnClientReceive), Me.Client)
                    Else
                        Close("No more data to receive,this connection will be closed.(S/C)")
                        Return
                    End If
                End If
            Else
                Close("Connection closed.(S/C)")
                Return
            End If
        Catch ex As Exception
            Close(ex.Message)
            Return
        End Try
    End Sub

    Private Sub connectimeoutwatcher()
        Thread.Sleep(10000)
        If Server IsNot Nothing Then
            If Server.Connected = False Then
                Close("Connect Timeout")
                Return
            End If
        Else
            Close("Connect Timeout")
            Return
        End If
    End Sub

    Private Sub OnRemoteSSLReceive(ar As IAsyncResult)
        Try
            If (Me.Server IsNot Nothing) AndAlso (Me.Client IsNot Nothing) AndAlso Me.Server.Connected AndAlso Me.Client.Connected AndAlso (Me.ClientSSLStream IsNot Nothing) AndAlso (Me.ServerSSLStream IsNot Nothing) Then
                Dim SSLStr As SslStream = TryCast(ar.AsyncState, SslStream)
                If SSLStr.CanRead Then
                    Dim size As Integer = SSLStr.EndRead(ar)
                    If size > 0 Then
                        Dim SendBuffer As Byte() = New Byte(size - 1) {}
                        Array.Copy(iRemoteBuffer, SendBuffer, size)
                        If ClientSSLStream.CanWrite Then
                            Me.ClientSSLStream.Write(SendBuffer)
                        End If
                        If BaseServiceProvider.Working = True Then Me.ServerSSLStream.BeginRead(iRemoteBuffer, 0, iRemoteBuffer.Length, New AsyncCallback(AddressOf Me.OnRemoteSSLReceive), SSLStr) Else Close("Receive Error:service has closed")
                    Else
                        Close("No more data to receive,this connection will be closed.(S/S)")
                        Return
                    End If
                End If
            Else
                Close("Connection closed.(S/S)")
                Return
            End If
        Catch ex As Exception
            Close(ex.Message)
            Return
        End Try
    End Sub

    Private Sub OnClientSSLReceive(ar As IAsyncResult)
        Try
            If (Me.Server IsNot Nothing) AndAlso (Me.Client IsNot Nothing) AndAlso Me.Server.Connected AndAlso Me.Client.Connected AndAlso (Me.ClientSSLStream IsNot Nothing) AndAlso (Me.ServerSSLStream IsNot Nothing) Then
                Dim SSLStr As SslStream = TryCast(ar.AsyncState, SslStream)
                If SSLStr.CanRead Then
                    Dim size As Integer = SSLStr.EndRead(ar)
                    If size > 0 Then
                        Dim SendBuffer As Byte() = New Byte(size - 1) {}
                        Array.Copy(iClientBuffer, SendBuffer, size)
                        If ServerSSLStream.CanWrite Then
                            If HostName.ToLower = "webapi.amap.com" AndAlso Encoding.UTF8.GetString(SendBuffer) Like "GET /maps/ipLocation*" Then
                                Dim sl As String = Encoding.UTF8.GetString(SendBuffer).Split(vbCrLf)(0).Trim.Replace("GET /maps/ipLocation", "").Split("&")(1).Replace("callback=", "")
                                sl += "({""info"":""LOCATE_SUCCESS"",""status"":1,""lng"":""" & BaseServiceProvider.lng & """,""lat"":""" & BaseServiceProvider.lat & """});"
                                ClientSSLStream.Write(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK
content-type: application/javascript;charset=utf-8
content-length: " & Encoding.UTF8.GetBytes(sl).Length & "
accept-ranges: bytes
cache-control: no-store
x-readtime: 49
access-control-allow-origin: *
access-control-allow-headers: *
access-control-allow-methods: *
server: Tengine/Aserver
strict-transport-security: max-age=0
timing-allow-origin: *

" & sl))
                            Else
                                Me.ServerSSLStream.Write(SendBuffer)
                            End If


                        End If
                        If BaseServiceProvider.Working = True Then Me.ClientSSLStream.BeginRead(iClientBuffer, 0, iClientBuffer.Length, New AsyncCallback(AddressOf Me.OnClientSSLReceive), SSLStr) Else Close("Receive Error:service has closed")
                    Else
                        Close("No more data to receive,this connection will be closed.(S/C)")
                        Return
                    End If
                End If
            Else
                Close("Connection closed.(S/C)")
                Return
            End If
        Catch ex As Exception
            Close(ex.Message)
            Return
        End Try
    End Sub

    Private Function ValidateServerCertificate(sender As Object, certificate As X509Certificate, chain As X509Chain, sslPolicyErrors As SslPolicyErrors) As Boolean

        If sslPolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch Then
            Dim cert2 As New X509Certificate2(certificate)
            For Each i As X509Extension In cert2.Extensions
                If i.Oid.Value = "2.5.29.17" Then
                    Dim t As String = Encoding.ASCII.GetString(i.RawData)
                    Dim nl As String() = t.Split("?")
                    For j = 0 To nl.Length - 1
                        If nl(j).Length >= 1 AndAlso HostName Like nl(j).Remove(0, 1) Then
                            Return True
                        End If
                    Next
                End If
            Next
            Try
                Dim a As String = cert2.SubjectName.Name
                Dim k As String() = a.Split(",")
                For Each item As String In k
                    If InStr(item, "CN=") Then
                        If HostName Like item.Replace("CN=", "").Trim Then
                            Return True
                        End If
                    End If
                Next
            Catch ex As Exception
                Return False
            End Try
            Return False
        ElseIf sslPolicyErrors = SslPolicyErrors.None Then
            Return True
        Else
            Return False
        End If

    End Function

    Private Sub AlsHead(client As Socket)
        Try
            Dim ns As New NetworkStream(client)
            Dim sr As New StreamReader(ns)
            Dim read As String = sr.ReadLine
            Dim Method As String = ""
            If (read IsNot Nothing) Then
                Method = read.Split(" ")(0).Trim
                HostName = read.Split(" ")(1).Trim
                Port = CInt(HostName.Split(":")(1))
                HostName = HostName.Split(":")(0)
                While (sr.ReadLine().Trim <> "")
                End While
            End If
            If Method.ToUpper.Trim = "CONNECT" Then
            Else
                Close("The connection is not a valid HTTPS connection.")
            End If
        Catch ex As Exception
            Close("The connection is not a valid HTTPS connection:" & ex.Message)
        End Try

    End Sub

    Public Sub Close(message As String)
        Try
            If ServerSSLStream IsNot Nothing Then
                ServerSSLStream.Close()
                ServerSSLStream.Dispose()
                ServerSSLStream = Nothing
            End If
        Catch
        End Try
        Try
            If ClientSSLStream IsNot Nothing Then
                ClientSSLStream.Close()
                ClientSSLStream.Dispose()
                ClientSSLStream = Nothing
            End If
        Catch
        End Try
        Try
            If Client IsNot Nothing Then
                Client.Close()
                Client.Dispose()
                Client = Nothing
            End If
        Catch
        End Try
        Try
            If Server IsNot Nothing Then
                Server.Close()
                Server.Dispose()
                Server = Nothing
            End If
        Catch
        End Try
        If CTS Then BaseServiceProvider.DeleteConnectionInfo(Key)
        Finalize()
    End Sub


End Class
