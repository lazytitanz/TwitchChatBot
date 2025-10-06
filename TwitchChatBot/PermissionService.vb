Imports TwitchLib.Client.Events
Imports Spectre.Console

''' <summary>
''' Twitch user role hierarchy for command permissions
''' </summary>
Public Enum Role
    Everyone
    Subscriber
    Vip
    Moderator
    Broadcaster
End Enum

''' <summary>
''' Handles user role detection and permission validation for Twitch chat commands.
''' Implements hierarchical role system where higher roles inherit lower role permissions.
''' </summary>
Public Class PermissionService
    Public Function HasRole(e As OnMessageReceivedArgs, minRole As Role) As Boolean
        Dim cm = e.ChatMessage
        Select Case minRole
            Case Role.Everyone : Return True
            Case Role.Subscriber : Return cm.IsSubscriber OrElse cm.IsModerator OrElse cm.IsBroadcaster
            Case Role.Vip : Return cm.IsVip OrElse cm.IsModerator OrElse cm.IsBroadcaster
            Case Role.Moderator : Return cm.IsModerator OrElse cm.IsBroadcaster
            Case Role.Broadcaster : Return cm.IsBroadcaster
            Case Else : Return False
        End Select
    End Function

    Public Function GetUserRole(e As OnMessageReceivedArgs) As Role
        Dim cm = e.ChatMessage
        If cm.IsBroadcaster Then Return Role.Broadcaster
        If cm.IsModerator Then Return Role.Moderator
        If cm.IsVip Then Return Role.Vip
        If cm.IsSubscriber Then Return Role.Subscriber
        Return Role.Everyone
    End Function

    Public Function GetRoleName(role As Role) As String
        Select Case role
            Case Role.Broadcaster : Return "Broadcaster"
            Case Role.Moderator : Return "Moderator"
            Case Role.Vip : Return "VIP"
            Case Role.Subscriber : Return "Subscriber"
            Case Role.Everyone : Return "Everyone"
            Case Else : Return "Unknown"
        End Select
    End Function

    Public Sub LogPermissionDenied(username As String, command As String, userRole As Role, requiredRole As Role)
        AnsiConsole.MarkupLine($"[dim]→[/] [red]Denied:[/] {command.EscapeMarkup()} - {username.EscapeMarkup()} [dim]({GetRoleName(userRole)} < {GetRoleName(requiredRole)})[/]")
    End Sub
End Class