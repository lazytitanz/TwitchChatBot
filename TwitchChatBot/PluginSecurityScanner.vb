Imports Mono.Cecil
Imports Mono.Cecil.Cil
Imports System.IO
Imports System.Text.RegularExpressions
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class PluginSecurityScanner
    ' Network namespaces - must match by namespace exactly
    Private Shared ReadOnly NetworkNamespaces As String() = {
        "System.Net", "System.Net.Http", "System.Net.Sockets", "System.Net.WebSockets"
    }

    ' IO/Registry namespaces
    Private Shared ReadOnly IONamespaces As String() = {
        "System.IO"
    }

    ' Registry-specific types
    Private Shared ReadOnly RegistryTypes As String() = {
        "Microsoft.Win32.Registry", "Microsoft.Win32.RegistryKey"
    }

    ' Process namespace
    Private Shared ReadOnly ProcessNamespaces As String() = {
        "System.Diagnostics.Process"
    }

    ' Cryptography namespaces (not including Base64 conversion)
    Private Shared ReadOnly CryptoNamespaces As String() = {
        "System.Security.Cryptography"
    }

    ' Base64 specific methods
    Private Shared ReadOnly Base64Methods As String() = {
        "System.Convert.FromBase64String",
        "System.Convert.ToBase64String",
        "System.Convert.FromBase64CharArray",
        "System.Convert.ToBase64CharArray"
    }

    ' Dynamic loading methods - high risk
    Private Shared ReadOnly DynamicLoadMethods As String() = {
        "System.Reflection.Assembly.Load",
        "System.Reflection.Assembly.LoadFrom",
        "System.Reflection.Assembly.LoadFile",
        "System.Reflection.Assembly.LoadWithPartialName",
        "System.Activator.CreateInstance"
    }

    ' Process manipulation methods
    Private Shared ReadOnly ProcessMethods As String() = {
        "System.Diagnostics.Process.Start",
        "System.Diagnostics.Process.Kill"
    }

    ' Reflection.Emit namespace - code generation
    Private Shared ReadOnly ReflectionEmitNamespaces As String() = {
        "System.Reflection.Emit"
    }

    ' Interop/Marshal namespaces
    Private Shared ReadOnly InteropNamespaces As String() = {
        "System.Runtime.InteropServices.Marshal",
        "System.Runtime.InteropServices"
    }

    ' Known crypto library references
    Private Shared ReadOnly CryptoLibraries As String() = {
        "BouncyCastle", "Crypto", "Cipher"
    }

    ' API hosting / server methods - plugins should NOT host servers
    Private Shared ReadOnly APIHostingMethods As String() = {
        "System.Net.HttpListener.Start",
        "System.Net.HttpListener.GetContext",
        "System.Net.Sockets.TcpListener.Start",
        "System.Net.Sockets.Socket.Bind",
        "System.Net.Sockets.Socket.Listen",
        "Microsoft.AspNetCore.Builder.WebApplication.Run",
        "Microsoft.AspNetCore.Hosting.WebHostBuilderExtensions.Build"
    }

    ' API hosting types - check for instantiation
    Private Shared ReadOnly APIHostingTypes As String() = {
        "System.Net.HttpListener",
        "System.Net.Sockets.TcpListener",
        "Microsoft.AspNetCore.Builder.WebApplication",
        "Microsoft.Owin.Hosting.WebApp"
    }

    ' Category score caps to prevent inflation
    Private Const MaxObfuscationScore As Integer = 30
    Private Const MaxPInvokeScore As Integer = 25
    Private Const MaxNetworkScore As Integer = 20
    Private Const MaxReflectionScore As Integer = 20
    Private Const MaxProcessScore As Integer = 20
    Private Const MaxIOScore As Integer = 15
    Private Const MaxBase64Score As Integer = 15
    Private Const MaxCryptoScore As Integer = 10
    Private Const MaxInteropScore As Integer = 15
    Private Const MaxEmbeddedResourceScore As Integer = 25
    Private Const MaxAPIHostingScore As Integer = 40

    ' Trusted publishers cache
    Private Shared trustedPublishers As HashSet(Of String)
    Private Shared trustedPublishersLoaded As Boolean = False

    Private Shared Sub LoadTrustedPublishers()
        If trustedPublishersLoaded Then Return

        trustedPublishers = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trusted-publishers.json")

        If File.Exists(configPath) Then
            Try
                Dim json = File.ReadAllText(configPath)
                Dim config = JObject.Parse(json)
                Dim publishers = config("trustedPublishers")?.ToObject(Of List(Of String))()
                If publishers IsNot Nothing Then
                    For Each publisher In publishers
                        trustedPublishers.Add(publisher)
                    Next
                End If
            Catch ex As Exception
                ' If config fails to load, continue with empty trusted list
            End Try
        End If

        trustedPublishersLoaded = True
    End Sub

    Private Shared Function IsTrustedPublisher(manifest As PluginManifest) As Boolean
        LoadTrustedPublishers()
        If String.IsNullOrEmpty(manifest.Publisher) Then Return False
        Return trustedPublishers.Contains(manifest.Publisher)
    End Function

    Public Shared Function ScanPlugin(pluginPath As String, manifest As PluginManifest) As PluginScanResult
        Dim result As New PluginScanResult With {.PluginPath = pluginPath}
        Dim flags As New HashSet(Of String)
        Dim violations As New HashSet(Of String)
        Dim apiCalls As New HashSet(Of String)

        ' Track category scores
        Dim categoryScores As New Dictionary(Of String, Integer)

        Try
            Dim assembly = AssemblyDefinition.ReadAssembly(pluginPath)

            ' Get the plugin's own namespaces to exclude self-references
            Dim pluginNamespaces As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each [module] In assembly.Modules
                For Each type In [module].Types
                    If Not String.IsNullOrWhiteSpace(type.Namespace) Then
                        ' Add the root namespace
                        Dim rootNS = If(type.Namespace.Contains("."c),
                                       type.Namespace.Substring(0, type.Namespace.IndexOf("."c)),
                                       type.Namespace)
                        pluginNamespaces.Add(rootNS)
                    End If
                Next
            Next

            ' Check for obfuscation indicators
            result.IsObfuscated = DetectObfuscation(assembly)
            If result.IsObfuscated Then
                flags.Add("Obfuscation")
                categoryScores("Obfuscation") = MaxObfuscationScore
            End If

            ' Scan assembly references for crypto libraries
            ScanAssemblyReferences(assembly, flags, categoryScores)

            ' Scan embedded resources for large payloads
            ScanEmbeddedResources(assembly, flags, categoryScores)

            ' Scan all types and methods
            For Each [module] In assembly.Modules
                For Each type In [module].Types
                    ScanType(type, flags, violations, categoryScores, manifest, apiCalls, pluginNamespaces)
                Next
            Next

            ' Store detected API calls
            result.DetectedApiCalls = apiCalls

            ' Validate API calls against manifest allowedApis
            If apiCalls.Count > 0 Then
                For Each apiCall In apiCalls
                    Dim isAllowed = False
                    If manifest.AllowedApis IsNot Nothing Then
                        For Each allowedApi In manifest.AllowedApis
                            If apiCall.Equals(allowedApi, StringComparison.OrdinalIgnoreCase) Then
                                isAllowed = True
                                Exit For
                            End If
                        Next
                    End If

                    If Not isAllowed Then
                        result.UndeclaredApiCalls.Add(apiCall)
                    End If
                Next
            End If

            ' Compute final flags
            result.Flags.AddRange(flags)
            result.ViolatedCapabilities.AddRange(violations)

            ' Set HasBase64Blobs and HasEncryptedStrings based on specific flags
            result.HasBase64Blobs = flags.Contains("Base64") OrElse flags.Contains("Large Base64 blob")
            result.HasEncryptedStrings = flags.Contains("High-entropy strings")

            ' Calculate total score
            result.RiskScore = categoryScores.Values.Sum()

            ' Combo scoring - increase risk for dangerous combinations
            If flags.Contains("Network") AndAlso flags.Contains("IO") AndAlso
               (flags.Contains("Base64") OrElse flags.Contains("Crypto")) Then
                result.RiskScore += 30 ' Network + IO + encoding = data exfiltration risk
                flags.Add("High-risk combo: Network+IO+Encoding")
            End If

            If flags.Contains("Network") AndAlso flags.Contains("Process") Then
                result.RiskScore += 25 ' Network + Process = potential C2
                flags.Add("High-risk combo: Network+Process")
            End If

            If flags.Contains("Dynamic loading") AndAlso flags.Contains("Network") Then
                result.RiskScore += 20 ' Remote code execution risk
                flags.Add("High-risk combo: Dynamic loading+Network")
            End If

            result.CalculateRiskLevel()

            ' Check if plugin is from a trusted publisher
            Dim isTrusted = IsTrustedPublisher(manifest)
            If isTrusted Then
                flags.Add("Trusted publisher")
            End If

            ' Block-list checks (automatic failures)
            If result.UndeclaredApiCalls.Count > 0 Then
                result.IsApproved = False
                result.DenialReason = $"BLOCKED: Undeclared API calls detected: {String.Join(", ", result.UndeclaredApiCalls.Take(5))}"
                result.RiskLevel = RiskLevel.High
            ElseIf flags.Contains("Assembly.Load(byte[])") Then
                ' Even trusted publishers cannot use Assembly.Load(byte[])
                If Not (isTrusted AndAlso manifest.Capabilities.DynamicLoading) Then
                    result.IsApproved = False
                    result.DenialReason = "BLOCKED: Assembly.Load(byte[]) detected - dynamic code loading from byte arrays is prohibited"
                    result.RiskLevel = RiskLevel.High
                End If
            ElseIf flags.Contains("API hosting") Then
                ' Trusted publishers can use API hosting if declared
                If Not (isTrusted AndAlso manifest.Capabilities.ApiHosting) Then
                    result.IsApproved = False
                    result.DenialReason = "BLOCKED: API hosting/server functionality requires trusted publisher status and 'apiHosting' capability"
                    result.RiskLevel = RiskLevel.High
                End If
            ElseIf flags.Contains("Reflection.Emit") Then
                ' Trusted publishers can use Reflection.Emit if declared
                If Not (isTrusted AndAlso manifest.Capabilities.ReflectionEmit) Then
                    result.IsApproved = False
                    result.DenialReason = "BLOCKED: Reflection.Emit requires trusted publisher status and 'reflectionEmit' capability"
                    result.RiskLevel = RiskLevel.High
                End If
            ElseIf flags.Contains("P/Invoke") AndAlso Not manifest.Capabilities.Process Then
                result.IsApproved = False
                result.DenialReason = "BLOCKED: P/Invoke requires 'process' capability"
                result.RiskLevel = RiskLevel.High
            ElseIf flags.Contains("Process") AndAlso Not manifest.Capabilities.Process Then
                result.IsApproved = False
                result.DenialReason = "BLOCKED: Process manipulation requires 'process' capability"
                result.RiskLevel = RiskLevel.High
            ElseIf violations.Count > 0 Then
                result.IsApproved = False
                result.DenialReason = $"Capability violations: {String.Join("; ", violations.Take(3))}"
            End If

        Catch ex As Exception
            result.IsApproved = False
            result.DenialReason = $"Scan failed: {ex.Message}"
            result.RiskLevel = RiskLevel.High
        End Try

        Return result
    End Function

    Private Shared Function DetectObfuscation(assembly As AssemblyDefinition) As Boolean
        Dim obfuscationIndicators = 0

        For Each [module] In assembly.Modules
            For Each type In [module].Types
                ' Check for obfuscated type names
                If IsObfuscatedName(type.Name) Then
                    obfuscationIndicators += 1
                End If

                ' Check for obfuscation attributes
                For Each attr In type.CustomAttributes
                    If attr.AttributeType.FullName.Contains("Obfuscation") Then
                        obfuscationIndicators += 2
                    End If
                Next

                ' Check for obfuscated method names
                For Each method In type.Methods
                    If IsObfuscatedName(method.Name) Then
                        obfuscationIndicators += 1
                    End If
                Next

                ' Early exit for performance
                If obfuscationIndicators > 5 Then
                    Return True
                End If
            Next
        Next

        Return obfuscationIndicators > 3
    End Function

    Private Shared Function IsObfuscatedName(name As String) As Boolean
        ' Skip compiler-generated and special names
        If name.StartsWith("<") OrElse name.StartsWith("_Lambda$") OrElse
           name.Contains("$VB$") OrElse name.Contains("__") Then
            Return False
        End If

        ' Skip common method names
        If name = "ToString" OrElse name = "GetHashCode" OrElse name = "Equals" OrElse
           name = "GetType" OrElse name = ".ctor" OrElse name = ".cctor" Then
            Return False
        End If

        ' Single character names (except common ones like x, y, i, j)
        If name.Length = 1 Then
            Return Not "xyzijkabcmnpqt".Contains(Char.ToLower(name(0)))
        End If

        ' Check for randomized names (high consonant density)
        If name.Length >= 3 AndAlso name.Length <= 8 Then
            Dim consonantCount = name.Count(Function(c) "bcdfghjklmnpqrstvwxz".Contains(Char.ToLower(c)))
            If consonantCount >= name.Length * 0.75 Then ' 75% consonants
                Return True
            End If
        End If

        Return False
    End Function

    Private Shared Sub ScanAssemblyReferences(assembly As AssemblyDefinition, flags As HashSet(Of String), scores As Dictionary(Of String, Integer))
        For Each reference In assembly.MainModule.AssemblyReferences
            Dim refName = reference.Name
            For Each cryptoLib In CryptoLibraries
                If refName.Contains(cryptoLib) Then
                    flags.Add("Crypto library reference")
                    If Not scores.ContainsKey("Crypto") Then
                        scores("Crypto") = Math.Min(5, MaxCryptoScore)
                    End If
                    Exit For
                End If
            Next
        Next
    End Sub

    Private Shared Sub ScanEmbeddedResources(assembly As AssemblyDefinition, flags As HashSet(Of String), scores As Dictionary(Of String, Integer))
        For Each [module] In assembly.Modules
            For Each resource In [module].Resources
                Dim embeddedResource = TryCast(resource, EmbeddedResource)
                If embeddedResource IsNot Nothing Then
                    ' Flag large embedded resources (>100KB)
                    Dim size = embeddedResource.GetResourceData().Length
                    If size > 102400 Then ' 100KB
                        flags.Add($"Large embedded resource ({size \ 1024}KB)")
                        If Not scores.ContainsKey("EmbeddedResource") Then
                            scores("EmbeddedResource") = MaxEmbeddedResourceScore
                        End If
                    End If
                End If
            Next
        Next
    End Sub

    Private Shared Sub ScanType(type As TypeDefinition, flags As HashSet(Of String),
                                violations As HashSet(Of String), scores As Dictionary(Of String, Integer),
                                manifest As PluginManifest, apiCalls As HashSet(Of String), pluginNamespaces As HashSet(Of String))
        For Each method In type.Methods
            If Not method.HasBody Then
                ' Check for P/Invoke via IsPInvokeImpl
                If method.IsPInvokeImpl Then
                    flags.Add("P/Invoke")
                    If Not scores.ContainsKey("PInvoke") Then
                        scores("PInvoke") = MaxPInvokeScore
                    End If
                    If Not manifest.Capabilities.Process Then
                        violations.Add("P/Invoke without 'process' capability")
                    End If
                End If
                Continue For
            End If

            ' Check for DllImport attribute
            For Each attr In method.CustomAttributes
                If attr.AttributeType.FullName = "System.Runtime.InteropServices.DllImportAttribute" Then
                    flags.Add("P/Invoke")
                    If Not scores.ContainsKey("PInvoke") Then
                        scores("PInvoke") = MaxPInvokeScore
                    End If
                    If Not manifest.Capabilities.Process Then
                        violations.Add("DllImport without 'process' capability")
                    End If
                End If
            Next

            ScanMethod(method, flags, violations, scores, manifest, apiCalls, pluginNamespaces)
        Next
    End Sub

    Private Shared Sub ScanMethod(method As MethodDefinition, flags As HashSet(Of String),
                                  violations As HashSet(Of String), scores As Dictionary(Of String, Integer),
                                  manifest As PluginManifest, apiCalls As HashSet(Of String), pluginNamespaces As HashSet(Of String))
        For Each instruction In method.Body.Instructions
            If instruction.OpCode = OpCodes.Call OrElse instruction.OpCode = OpCodes.Callvirt OrElse
               instruction.OpCode = OpCodes.Newobj Then
                Dim methodRef = TryCast(instruction.Operand, MethodReference)
                If methodRef IsNot Nothing Then
                    CheckMethodCall(methodRef, flags, violations, scores, manifest, apiCalls, pluginNamespaces)
                End If
            ElseIf instruction.OpCode = OpCodes.Ldstr Then
                Dim str = TryCast(instruction.Operand, String)
                If str IsNot Nothing Then
                    CheckSuspiciousString(str, flags, scores)
                End If
            End If
        Next
    End Sub

    Private Shared Sub CheckMethodCall(methodRef As MethodReference, flags As HashSet(Of String),
                                      violations As HashSet(Of String), scores As Dictionary(Of String, Integer),
                                      manifest As PluginManifest, Optional apiCalls As HashSet(Of String) = Nothing,
                                      Optional pluginNamespaces As HashSet(Of String) = Nothing)
        Dim declaringNS = methodRef.DeclaringType.Namespace
        Dim fullName = methodRef.DeclaringType.FullName & "." & methodRef.Name

        ' Track all external API calls (non-System, non-TwitchChatBot namespaces)
        If apiCalls IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(declaringNS) Then
            ' Extract root namespace first (e.g., "Newtonsoft" from "Newtonsoft.Json.Linq")
            Dim rootNS = If(declaringNS.Contains("."c), declaringNS.Substring(0, declaringNS.IndexOf("."c)), declaringNS)

            ' Check if this is a plugin's own namespace
            Dim isPluginNamespace = False
            If pluginNamespaces IsNot Nothing Then
                isPluginNamespace = pluginNamespaces.Contains(rootNS)
            End If

            ' Skip if it's a framework namespace, bot namespace, or the plugin's own namespace
            If Not declaringNS.StartsWith("System") AndAlso
               Not declaringNS.StartsWith("Microsoft") AndAlso
               Not declaringNS.StartsWith("TwitchChatBot") AndAlso
               Not declaringNS.StartsWith("TwitchLib") AndAlso
               Not declaringNS.StartsWith("Spectre.Console") AndAlso
               Not isPluginNamespace Then

                If Not String.IsNullOrWhiteSpace(rootNS) Then
                    apiCalls.Add(rootNS)
                End If
            End If
        End If

        ' Network checks - namespace-based
        For Each netNS In NetworkNamespaces
            If declaringNS IsNot Nothing AndAlso declaringNS.StartsWith(netNS) Then
                flags.Add("Network")
                If Not scores.ContainsKey("Network") Then
                    scores("Network") = MaxNetworkScore
                End If
                If Not manifest.Capabilities.Network Then
                    violations.Add($"Network: {fullName}")
                End If
                Exit For
            End If
        Next

        ' IO checks - namespace-based
        For Each ioNS In IONamespaces
            If declaringNS IsNot Nothing AndAlso declaringNS.StartsWith(ioNS) Then
                flags.Add("IO")
                If Not scores.ContainsKey("IO") Then
                    scores("IO") = MaxIOScore
                End If
                If Not manifest.Capabilities.Disk Then
                    violations.Add($"File I/O: {fullName}")
                End If
                Exit For
            End If
        Next

        ' Registry checks
        For Each regType In RegistryTypes
            If fullName.StartsWith(regType) Then
                flags.Add("Registry")
                If Not scores.ContainsKey("Registry") Then
                    scores("Registry") = MaxIOScore
                End If
                If Not manifest.Capabilities.Disk Then
                    violations.Add($"Registry: {fullName}")
                End If
                Exit For
            End If
        Next

        ' Process checks - exact method matching
        For Each procMethod In ProcessMethods
            If fullName.StartsWith(procMethod) Then
                flags.Add("Process")
                If Not scores.ContainsKey("Process") Then
                    scores("Process") = MaxProcessScore
                End If
                If Not manifest.Capabilities.Process Then
                    violations.Add($"Process: {fullName}")
                End If
                Exit For
            End If
        Next

        ' Crypto checks - namespace-based (separate from Base64)
        For Each cryptoNS In CryptoNamespaces
            If declaringNS IsNot Nothing AndAlso declaringNS.StartsWith(cryptoNS) Then
                flags.Add("Crypto")
                If Not scores.ContainsKey("Crypto") Then
                    scores("Crypto") = MaxCryptoScore
                End If
                Exit For
            End If
        Next

        ' Base64 checks - exact method matching
        For Each base64Method In Base64Methods
            If fullName = base64Method Then
                flags.Add("Base64")
                If Not scores.ContainsKey("Base64") Then
                    scores("Base64") = MaxBase64Score
                End If
                Exit For
            End If
        Next

        ' Dynamic loading methods - exact matching
        For Each dynMethod In DynamicLoadMethods
            If fullName.StartsWith(dynMethod) Then
                flags.Add("Dynamic loading")
                If Not scores.ContainsKey("Reflection") Then
                    scores("Reflection") = MaxReflectionScore
                End If

                ' Special check for Assembly.Load(byte[])
                If methodRef.Name = "Load" AndAlso methodRef.Parameters.Count = 1 AndAlso
                   methodRef.Parameters(0).ParameterType.FullName = "System.Byte[]" Then
                    flags.Add("Assembly.Load(byte[])")
                End If
                Exit For
            End If
        Next

        ' Reflection.Emit - namespace check (code generation)
        For Each emitNS In ReflectionEmitNamespaces
            If declaringNS IsNot Nothing AndAlso declaringNS.StartsWith(emitNS) Then
                flags.Add("Reflection.Emit")
                If Not scores.ContainsKey("Emit") Then
                    scores("Emit") = MaxReflectionScore
                End If
                Exit For
            End If
        Next

        ' Interop/Marshal checks
        For Each interopNS In InteropNamespaces
            If declaringNS IsNot Nothing AndAlso declaringNS.StartsWith(interopNS) Then
                flags.Add("Interop/Marshal")
                If Not scores.ContainsKey("Interop") Then
                    scores("Interop") = MaxInteropScore
                End If
                Exit For
            End If
        Next

        ' API hosting checks - plugins should NOT host servers
        For Each hostMethod In APIHostingMethods
            If fullName.StartsWith(hostMethod) Then
                flags.Add("API hosting")
                If Not scores.ContainsKey("APIHosting") Then
                    scores("APIHosting") = MaxAPIHostingScore
                End If
                Exit For
            End If
        Next

        ' Check for instantiation of API hosting types
        If methodRef.Name = ".ctor" Then
            For Each hostType In APIHostingTypes
                If methodRef.DeclaringType.FullName.StartsWith(hostType) Then
                    flags.Add("API hosting")
                    If Not scores.ContainsKey("APIHosting") Then
                        scores("APIHosting") = MaxAPIHostingScore
                    End If
                    Exit For
                End If
            Next
        End If
    End Sub

    Private Shared Sub CheckSuspiciousString(str As String, flags As HashSet(Of String), scores As Dictionary(Of String, Integer))
        ' Lenient Base64 regex - check for long Base64-like strings
        If str.Length >= 100 Then
            If Regex.IsMatch(str, "^[A-Za-z0-9+/]{90,}={0,2}$") Then
                flags.Add("Large Base64 blob")
                If Not scores.ContainsKey("Base64") Then
                    scores("Base64") = MaxBase64Score
                End If
            End If
        End If

        ' Shannon entropy check for encrypted/encoded strings
        If str.Length >= 50 AndAlso CalculateShannonEntropy(str) > 4.5 Then
            flags.Add("High-entropy strings")
            If Not scores.ContainsKey("HighEntropy") Then
                scores("HighEntropy") = 15
            End If
        End If
    End Sub

    Private Shared Function CalculateShannonEntropy(str As String) As Double
        ' Calculate Shannon entropy: -Σ(p(x) * log2(p(x)))
        Dim frequency As New Dictionary(Of Char, Integer)

        For Each c In str
            If frequency.ContainsKey(c) Then
                frequency(c) += 1
            Else
                frequency(c) = 1
            End If
        Next

        Dim entropy As Double = 0.0
        Dim length = str.Length

        For Each kvp In frequency
            Dim probability = CDbl(kvp.Value) / length
            entropy -= probability * Math.Log(probability, 2)
        Next

        Return entropy
    End Function
End Class
