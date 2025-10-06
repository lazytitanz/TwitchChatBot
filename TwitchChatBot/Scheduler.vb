Imports System.Threading
Imports TwitchLib.Client
Imports Spectre.Console

''' <summary>
''' Lightweight timer-based scheduler for running commands at regular intervals.
''' Manages periodic execution of scheduled tasks like automatic messages.
''' </summary>
Public Class Scheduler
    Private ReadOnly jobs As New List(Of ScheduledJob)
    Private ReadOnly jobLock As New Object
    Private timer As Timer

    ''' <summary>
    ''' Schedule a command to run at regular intervals
    ''' </summary>
    Public Sub ScheduleJob(name As String, intervalMinutes As Integer, command As IScheduledCommand)
        SyncLock jobLock
            Dim job As New ScheduledJob(name, intervalMinutes, command)
            jobs.Add(job)
            AnsiConsole.MarkupLine($"  [dim]└─[/] [green]Scheduled:[/] {name.EscapeMarkup()} [dim](every {intervalMinutes}m)[/]")
        End SyncLock
    End Sub

    ''' <summary>
    ''' Remove a scheduled job by name
    ''' </summary>
    Public Sub RemoveJob(name As String)
        SyncLock jobLock
            Dim job = jobs.FirstOrDefault(Function(j) j.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            If job IsNot Nothing Then
                jobs.Remove(job)
                AnsiConsole.MarkupLine($"  [dim]└─[/] [yellow]Removed job:[/] {name.EscapeMarkup()}[/]")
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' Check and execute any jobs that are due to run
    ''' </summary>
    Public Sub ProcessJobs(client As TwitchClient, channel As String)
        Dim now As DateTime = DateTime.UtcNow
        Dim jobsToRun As New List(Of ScheduledJob)

        SyncLock jobLock
            For Each job In jobs
                If job.IsDue(now) Then
                    jobsToRun.Add(job)
                    job.UpdateLastRun(now)
                End If
            Next
        End SyncLock

        For Each job In jobsToRun
            Try
                job.Command.Execute(client, channel)
                AnsiConsole.MarkupLine($"[dim]⏰ Scheduled: {job.Name.EscapeMarkup()}[/]")
            Catch ex As Exception
                AnsiConsole.MarkupLine($"[red]✗ Job error ({job.Name.EscapeMarkup()}): {ex.Message.EscapeMarkup()}[/]")
            End Try
        Next
    End Sub

    ''' <summary>
    ''' Get list of all scheduled jobs
    ''' </summary>
    Public Function GetJobs() As ScheduledJob()
        SyncLock jobLock
            Return jobs.ToArray()
        End SyncLock
    End Function

    ''' <summary>
    ''' Start the scheduler with automatic job processing
    ''' </summary>
    Public Sub Start(client As TwitchClient, channel As String)
        timer = New Timer(
            Sub() ProcessJobs(client, channel),
            Nothing,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1)
        )
        AnsiConsole.MarkupLine("  [dim]└─[/] [blue]Scheduler started[/] [dim](checking every 1m)[/]")
    End Sub

    ''' <summary>
    ''' Stop the scheduler and dispose of resources
    ''' </summary>
    Public Sub StopScheduler()
        If timer IsNot Nothing Then
            timer.Dispose()
            timer = Nothing
            AnsiConsole.MarkupLine("  [dim]└─[/] [yellow]Scheduler stopped[/]")
        End If
    End Sub
End Class

''' <summary>
''' Represents a single scheduled job with timing and execution details
''' </summary>
Public Class ScheduledJob
    Public ReadOnly Property Name As String
    Public ReadOnly Property IntervalMinutes As Integer
    Public ReadOnly Property Command As IScheduledCommand
    Private lastRun As DateTime

    Public Sub New(name As String, intervalMinutes As Integer, command As IScheduledCommand)
        Me.Name = name
        Me.IntervalMinutes = intervalMinutes
        Me.Command = command
        ' Set lastRun to past time so first execution happens on next check
        Me.lastRun = DateTime.UtcNow.AddMinutes(-intervalMinutes)
    End Sub

    Public Function IsDue(currentTime As DateTime) As Boolean
        Return (currentTime - lastRun).TotalMinutes >= IntervalMinutes
    End Function

    Public Sub UpdateLastRun(currentTime As DateTime)
        lastRun = currentTime
    End Sub

    Public Function GetNextRunTime() As DateTime
        Return lastRun.AddMinutes(IntervalMinutes)
    End Function
End Class

''' <summary>
''' Interface for commands that can be scheduled to run automatically
''' </summary>
Public Interface IScheduledCommand
    ''' <summary>Execute the scheduled command</summary>
    Sub Execute(client As TwitchClient, channel As String)
End Interface