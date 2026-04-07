using System.Collections.Generic;

namespace VastPrint.App.Models;

public sealed record FileDiscoveryResult(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);
