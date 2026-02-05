# .NET Diagnostic Code Collector

A tool that extracts diagnostic codes (errors, warnings, obsoletions) from .NET repositories and produces structured JSON files for programmatic consumption.

## Purpose

When building tooling around .NET diagnostics (IDE extensions, documentation generators, error lookup services), you need structured access to diagnostic metadata. This tool:

1. Parses source code from official .NET repositories
2. Extracts diagnostic IDs, messages, categories, and documentation URLs
3. Outputs one JSON file per diagnostic prefix for efficient lookup
4. Generates an `index.json` manifest for consumer discovery

## Output Design

### Per-Prefix JSON Files

Each diagnostic prefix gets its own file (e.g., `cs.json`, `netsdk.json`). This design enables:

- **Efficient loading**: Consumers only fetch the prefix they need
- **Clear namespacing**: Each file represents one "namespace" of codes
- **Simple caching**: Files can be cached independently

Example file structure:
```
errors/
├── index.json      # Manifest mapping prefixes to files
├── cs.json         # C# compiler diagnostics (CS0001-CS9999)
├── syslib.json     # Runtime obsoletions/analyzers (SYSLIB0001-SYSLIB5999)
├── netsdk.json     # SDK build errors (NETSDK1001+)
├── ca.json         # Code analysis rules (CA1000+)
├── rz.json         # Razor compiler diagnostics (RZ0000+)
└── ...
```

### index.json Schema

```json
{
  "version": "1.0",
  "generatedAt": "2026-02-05T00:45:00Z",
  "prefixes": {
    "CS": {
      "file": "cs.json",
      "repo": "roslyn",
      "pattern": "^CS\\d{4}$",
      "count": 1969,
      "description": "C# compiler errors and warnings"
    }
  }
}
```

### Per-Prefix File Schema

```json
{
  "prefix": "CS",
  "repo": "roslyn",
  "description": "C# compiler errors and warnings",
  "generatedAt": "2026-02-05T00:45:00Z",
  "diagnostics": [
    {
      "id": "CS0001",
      "category": "ERR",
      "name": "ERR_BadBinaryOps",
      "message": "Operator '{0}' cannot be applied to operands of type '{1}' and '{2}'",
      "helpUrl": "https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/cs0001"
    }
  ]
}
```

## Supported Repositories

| Repository | Prefixes | Count | Description |
|------------|----------|-------|-------------|
| [roslyn](https://github.com/dotnet/roslyn) | CS | 1,969 | C# compiler errors and warnings |
| [runtime](https://github.com/dotnet/runtime) | SYSLIB | 170 | Obsoletions, analyzers, experimental APIs |
| [sdk](https://github.com/dotnet/sdk) | NETSDK, CA | 516 | Build errors, code analysis rules |
| [aspnetcore](https://github.com/dotnet/aspnetcore) | ASP, BL, MVC, API, RDG, SSG | 80 | ASP.NET Core analyzers |
| [efcore](https://github.com/dotnet/efcore) | EF | 13 | Entity Framework Core diagnostics |
| [aspire](https://github.com/dotnet/aspire) | ASPIRE | 8 | Aspire hosting diagnostics |
| [extensions](https://github.com/dotnet/extensions) | LOGGEN, METGEN, CTXOPTGEN, EXTEXP, EXTOBS | 77 | Extensions generators and analyzers |
| [msbuild](https://github.com/dotnet/msbuild) | MSB, BC | 705 | MSBuild errors and BuildCheck diagnostics |
| [razor](https://github.com/dotnet/razor) | RZ | 122 | Razor compiler diagnostics |

**Total: 3,660 diagnostics across 20 prefixes**

## Usage

### Prerequisites

- .NET 10 SDK
- Clone the source repositories you want to collect from

### Running the Collector

```bash
cd DiagnosticCollector

# Collect from all repositories
dotnet run -- \
  --roslyn ~/git/roslyn \
  --runtime ~/git/runtime \
  --sdk ~/git/sdk \
  --aspnetcore ~/git/aspnetcore \
  --efcore ~/git/efcore \
  --aspire ~/git/aspire \
  --extensions ~/git/extensions \
  --msbuild ~/git/msbuild \
  --razor ~/git/razor \
  -o ../errors

# Or collect from specific repositories
dotnet run -- --roslyn ~/git/roslyn --runtime ~/git/runtime -o ../errors
```

### Command Line Options

| Option | Description |
|--------|-------------|
| `--roslyn <path>` | Path to roslyn repository |
| `--runtime <path>` | Path to runtime repository |
| `--sdk <path>` | Path to sdk repository |
| `--aspnetcore <path>` | Path to aspnetcore repository |
| `--efcore <path>` | Path to efcore repository |
| `--aspire <path>` | Path to aspire repository |
| `--extensions <path>` | Path to extensions repository |
| `--msbuild <path>` | Path to msbuild repository |
| `--razor <path>` | Path to razor repository |
| `--docs <path>` | Path to docs repository (for Roslyn doc links) |
| `-o, --output <path>` | Output directory (default: `./errors`) |
| `-h, --help` | Show help |

## Consumer Usage

To look up a diagnostic by ID:

1. **Fetch `index.json`** (cache it)
2. **Parse the diagnostic ID** to extract the prefix (e.g., `NETSDK1045` → `NETSDK`)
3. **Look up the prefix** in `index.json` to get the filename
4. **Fetch that file** and find the diagnostic by ID

Example (pseudocode):
```javascript
const index = await fetch('errors/index.json').then(r => r.json());
const prefix = diagnosticId.match(/^[A-Z]+/)[0];  // "NETSDK"
const file = index.prefixes[prefix].file;          // "netsdk.json"
const data = await fetch(`errors/${file}`).then(r => r.json());
const diagnostic = data.diagnostics.find(d => d.id === diagnosticId);
```

## Adding New Repositories

1. Create a new collector class in `DiagnosticCollector/Collectors/` implementing `ICollector`
2. Register it in `Program.cs`:
   - Add a CLI argument parser case
   - Add a config property
   - Instantiate the collector when the path is provided
3. Run the tool with the new `--repo` argument

## License

MIT
