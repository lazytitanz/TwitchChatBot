Public Enum RiskLevel
    Low
    Medium
    High
End Enum

Public Class PluginScanResult
    Public Property PluginPath As String
    Public Property IsObfuscated As Boolean
    Public Property HasEncryptedStrings As Boolean
    Public Property HasBase64Blobs As Boolean
    Public Property RiskLevel As RiskLevel
    Public Property RiskScore As Integer
    Public Property Flags As List(Of String)
    Public Property ViolatedCapabilities As List(Of String)
    Public Property DetectedApiCalls As HashSet(Of String)
    Public Property UndeclaredApiCalls As List(Of String)
    Public Property IsApproved As Boolean
    Public Property DenialReason As String

    Public Sub New()
        Flags = New List(Of String)()
        ViolatedCapabilities = New List(Of String)()
        DetectedApiCalls = New HashSet(Of String)()
        UndeclaredApiCalls = New List(Of String)()
        IsApproved = True
        RiskScore = 0
    End Sub

    Public Sub CalculateRiskLevel()
        If RiskScore >= 100 Then
            RiskLevel = RiskLevel.High
        ElseIf RiskScore >= 50 Then
            RiskLevel = RiskLevel.Medium
        Else
            RiskLevel = RiskLevel.Low
        End If
    End Sub

    Public Function GetSummary() As String
        Dim flagsStr = If(Flags.Count > 0, String.Join(", ", Flags), "None")
        Return $"Risk: {RiskLevel} (Score: {RiskScore}) | Flags: {flagsStr} | Approved: {IsApproved}"
    End Function
End Class
