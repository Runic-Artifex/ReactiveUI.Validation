// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;
using Runic.Validation.Verification;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks actual peer output without class-name lookalikes or same-round interception assumptions.</summary>
public sealed partial class RuleCapabilityCompilerTests
{
    /// <summary>The shared original source used by both compiler and actual-package gates.</summary>
    private const string PeerSourceFile = "PeerGeneratedCallerScenario.cs";

    /// <summary>The actual peer's retained output identity.</summary>
    private const string PeerHintName = "PeerRegisteredRuleCaller.g.cs";

    /// <summary>The SHA-256 hexadecimal digest length.</summary>
    private const int DigestLength = 64;

    /// <summary>The trusted consumer entry used only by the compiler host.</summary>
    private const string PeerEntryType = "PeerProbe";

    /// <summary>The compiler-only entry delegates to the exact shared shipping fixture.</summary>
    private const string PeerEntrySource = """
        public static class PeerProbe
        {
            public static bool Check() { PeerGeneratedCallerScenario.Run(); return true; }
        }
        """;

    /// <summary>Actual peer output uses both safe routes and rejects unknown or unregistered expressions.</summary>
    /// <param name="reactive">Whether to use System.Reactive packages.</param>
    /// <param name="validationFirst">Whether Validation runs before the real peer generator.</param>
    /// <returns>The asynchronous compiler/runtime assertion.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ActualPeerRegisteredCallerPreservesFiniteBoundary(bool reactive, bool validationFirst)
    {
        var prefix = reactive ? "#define REACTIVE_SHIM\n" : string.Empty;
        var source = $"{prefix}{await ReadPeerScenarioAsync()}\n{PeerEntrySource}";
        using var host = CapabilityCompilerHost.Create(reactive, validationFirst, additionalGenerators: [new PeerRegisteredCallerGenerator().AsSourceGenerator()]);
        var result = await host.RunAsync(source, PeerSourceFile);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        var emitted = result.GeneratedSources.Single(static generated => generated.HintName == PeerHintName);
        await Assert.That(emitted.Text).Contains(reactive ? ReactiveValidationRoot : PrimitiveValidationRoot);
        await Assert.That(emitted.Text).Contains("[ValidationRuntimeDispatch]");
        await Assert.That(emitted.Sha256.Length).IsEqualTo(DigestLength);
        await Assert.That(result.ExecuteBoolean(PeerEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Reads the checked-in source shared with managed, trimmed and native package acceptance.</summary>
    /// <returns>The original peer consumer source.</returns>
    /// <exception cref="FileNotFoundException">The repository fixture cannot be found from the test output.</exception>
    private static async Task<string> ReadPeerScenarioAsync()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "eng", "verification-fixtures", PeerSourceFile);
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }
        }

        throw new FileNotFoundException("The shared actual peer scenario was not found.", PeerSourceFile);
    }
}
