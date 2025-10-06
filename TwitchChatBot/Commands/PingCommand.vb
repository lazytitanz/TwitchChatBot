Imports TwitchLib.Client
Imports TwitchLib.Client.Events

Namespace Commands
    ''' <summary>
    ''' Simple ping command that responds with "pong" and a timestamp.
    ''' Used for testing bot connectivity and response time.
    ''' </summary>
    Public Class PingCommand
    Implements ICommand

    Public ReadOnly Property RequiredRole As Role Implements ICommand.RequiredRole
        Get
            Return Role.Everyone
        End Get
    End Property

    Public ReadOnly Property UserCooldownSeconds As Integer Implements ICommand.UserCooldownSeconds
        Get
                Return 0
            End Get
    End Property

    Public ReadOnly Property GlobalCooldownSeconds As Integer Implements ICommand.GlobalCooldownSeconds
        Get
            Return 0
        End Get
    End Property

    Public Sub Execute(client As TwitchClient, e As OnMessageReceivedArgs, args As String()) Implements ICommand.Execute
        Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss")
        client.SendMessage(e.ChatMessage.Channel, $"[{timestamp}] @{e.ChatMessage.Username} pong 🏓")
    End Sub
    End Class
End Namespace
