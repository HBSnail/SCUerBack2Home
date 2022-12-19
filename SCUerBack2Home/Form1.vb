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
Imports System.Security.Cryptography.X509Certificates

Public Class Form1
    Private GlobalCert As X509Certificate2
    Private FakeLocationServer As FakeLocationServer
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ComboBoxListenIP.Items.Clear()
        ComboBoxListenIP.Items.Add("127.0.0.1")
        ComboBoxListenIP.Items.Add("0.0.0.0")
        For Each i As IPAddress In Dns.GetHostAddresses(Dns.GetHostName)
            If i.AddressFamily = Sockets.AddressFamily.InterNetwork Then
                ComboBoxListenIP.Items.Add(i.ToString())
            End If
        Next
        ComboBoxListenIP.Text = "127.0.0.1"

        Dim certpath As String = "amapCert.pfx"
        Try
            Dim cert As X509Certificate2
            Dim x As String = Convert.ToBase64String(IO.File.ReadAllBytes("RootCA.crt"))
            cert = New X509Certificate2(certpath, "", X509KeyStorageFlags.PersistKeySet Or X509KeyStorageFlags.Exportable)
            If cert.HasPrivateKey = False Then
                Label3.Text = ("amapCert.pfx不包含私钥,无法发动中间人攻击。")
            End If
            GlobalCert = cert
            Label3.Text = "证书已加载:" + GlobalCert.FriendlyName

        Catch ex As Exception

        End Try
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        If GlobalCert IsNot Nothing Then
            Try

                FakeLocationServer = New FakeLocationServer(IPAddress.Parse(ComboBoxListenIP.Text), NumericUpDown1.Value, GlobalCert)
                FakeLocationServer.lng = TextBox1.Text
                FakeLocationServer.lat = TextBox2.Text
                FakeLocationServer.StartService()
                Panel1.Enabled = False
                Button2.Enabled = True
                Button1.Enabled = False

            Catch ex As Exception
                MsgBox("启动失败:" & ex.Message)
            End Try
        Else
            MsgBox("启动失败:证书读取失败")
        End If
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Try
            FakeLocationServer.StopService()
            FakeLocationServer = Nothing
            Panel1.Enabled = True
            Button2.Enabled = False
            Button1.Enabled = True
        Catch ex As Exception
            MsgBox("关闭失败:" & ex.Message)
        End Try
    End Sub

    Private Sub RadioButton1_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButton1.CheckedChanged
        If RadioButton1.Checked Then
            TextBox1.Text = "104.0598190"
            TextBox2.Text = "30.6008236"
        End If
    End Sub

    Private Sub RadioButton2_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButton2.CheckedChanged
        If RadioButton2.Checked Then
            TextBox1.Text = "104.1203320"
            TextBox2.Text = "30.6012700"
        End If
    End Sub

    Private Sub RadioButton3_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButton3.CheckedChanged
        If RadioButton3.Checked Then
            TextBox1.Text = "104.1121320"
            TextBox2.Text = "30.6071700"
        End If
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        MessageBox.Show("使用帮助" & vbCrLf & "1.将RootCA.crt导入计算机或浏览器的可信任根证书颁发机构列表" & vbCrLf & "2.修改浏览器HTTPS代理设置，指向回家小助手服务器的地址和端口" & vbCrLf & "3.访问并登录https://wfw.scu.edu.cn/ncov/wap/default/index进行正常健康报填报流程" & vbCrLf & "4.祝使用愉快，如有问题请在本项目GitHub下提交Issue。" & vbCrLf & vbCrLf & "工作原理" & vbCrLf & "本程序通过发动中间人攻击的方式拦截并伪造定位服务API的响应数据实现，不会以任何形式存储或发送您的隐私数据，请放心使用。")
    End Sub
End Class
