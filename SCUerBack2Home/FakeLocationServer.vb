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

Imports System.Net
Imports System.Net.Sockets
Imports System.Security.Cryptography.X509Certificates
Imports System.Threading

Friend Class FakeLocationServer
    Private ListenIP As Net.IPAddress
    Private ListenPort As Integer
    Public globalCert As X509Certificate2
    Public Working As Boolean
    Private Listener As TcpListener
    Private ConnectionTable As New Dictionary(Of String, HTTPSConnection)
    Friend lng As String
    Friend lat As String

    Public Sub New(iPAddress As Net.IPAddress, value As Decimal, globalCert As X509Certificate2)
        Me.ListenIP = iPAddress
        Me.ListenPort = value
        Me.globalCert = globalCert
    End Sub


    Private Sub OnSocketAccepted(ar As IAsyncResult)
        Dim iListener As TcpListener = TryCast(ar.AsyncState, TcpListener)
        Try
            Dim Client As Socket = iListener.EndAcceptSocket(ar)
            Dim CEndpoint As IPEndPoint = CType(Client.RemoteEndPoint, IPEndPoint)
            Dim key As String = CEndpoint.Address.ToString & ":" & CEndpoint.Port.ToString
            Dim Connection As HTTPSConnection = New HTTPSConnection(Me, Client, key)
            Try
                ConnectionTable.Add(key, Connection)
            Catch
            End Try
            ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf Connection.Open))
        Catch ex As Exception
        End Try
        Try
            iListener.BeginAcceptSocket(AddressOf OnSocketAccepted, iListener)
        Catch
        End Try
    End Sub



    Public Sub DeleteConnectionInfo(key As String)
        ConnectionTable.Remove(key)
    End Sub
    Public Sub StopService()
        Working = False
        Listener.Stop()
        Listener = Nothing
        For Each TC As HTTPSConnection In ConnectionTable.Values
            TC.CTS = False
            TC.Close("Service Closed")
        Next
        ConnectionTable.Clear()
        Finalize()
    End Sub

    Friend Sub StartService()
        Working = True
        Listener = New TcpListener(ListenIP, ListenPort)
        Listener.Start()
        Listener.BeginAcceptSocket(New AsyncCallback(AddressOf OnSocketAccepted), Listener)
    End Sub
End Class
