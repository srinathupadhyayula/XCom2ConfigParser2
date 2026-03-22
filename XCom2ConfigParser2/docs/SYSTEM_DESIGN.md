# System Design Document
## UE3 Config Parser & Validator (Desktop CLI)

**Version:** 1.0  
**Date:** 2026-03-22  
**Reference Implementation:** Rust `ue3-config-parser`

---

## 1. Architecture Overview

### 1.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      CLI Entry Point                        │
│                    (Program.cs / main.cpp)                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Command Line Parser                      │
│                   (System.CommandLine)                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     File System Walker                      │
│                  (Directory traversal, glob)                │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      File Reader                            │
│                 (UTF-8 validation, BOM strip)               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Parser Pipeline                          │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────────┐   │
│  │   Line      │ → │  Directive  │ → │   Validator     │   │
│  │   Splitter  │   │   Tokenizer │   │   (Rules)       │   │
│  └─────────────┘   └─────────────┘   └─────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                Struct Member Validator                       │
│  ┌────────────────┐  ┌────────────────┐  ┌──────────────┐  │
│  │ UC File Parser │  │ Build Script   │  │  Resolution  │  │
│  │ (struct defs)  │  │ Parser         │  │  Cache       │  │
│  └────────────────┘  └────────────────┘  └──────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Error Aggregator                          │
│              (Collect, format, output errors)               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Output Formatter                          │
│              (Console text / JSON / Summary)                │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| CLI Entry Point | Application startup, exit code handling |
| Command Line Parser | Parse args, validate options, show help |
| File System Walker | Recursively find files matching pattern |
| File Reader | Read file, validate UTF-8, strip BOM |
| Line Splitter | Split into lines, handle continuations (`\\`) |
| Directive Tokenizer | Classify lines as header/KVP/comment/unknown |
| Validator | Apply grammar rules, detect syntax errors |
| Struct Member Validator | Resolve struct definitions from UnrealScript sources and validate field names |
| Error Aggregator | Collect errors with spans |
| Output Formatter | Format errors for display |

### 1.3 Data Flow

```
File Path → Read Bytes → UTF-8 String → Lines → Directives → Syntax Validation → Struct Member Validation → Errors → Output
```

---

## 2. Data Structures

### 2.1 Core Types

#### Span
Represents a byte range in the source text.

```csharp
public readonly struct Span
{
    public int Start { get; }      // Byte offset of first character
    public int End { get; }        // Byte offset after last character
    public int Length => End - Start;
    
    public Span(int start, int end);
    public string Extract(string source) => source.Substring(Start, Length);
}
```

#### SourceLocation
Human-readable position (line/column).

```csharp
public readonly struct SourceLocation
{
    public int Line { get; }       // 1-based line number
    public int Column { get; }     // 1-based column number
    
    public SourceLocation(int line, int column);
}
```

#### SpanWithLocation
Combines byte span with start/end positions.

```csharp
public readonly struct SpanWithLocation
{
    public Span Span { get; }
    public SourceLocation Start { get; }
    public SourceLocation End { get; }
}
```

### 2.2 Directive Types

#### KvpOperation (enum)
```csharp
public enum KvpOperation
{
    Set = 0,          // No prefix
    InsertUnique = 1, // + prefix
    Insert = 2,       // . prefix
    Remove = 3,       // - prefix
    Clear = 4         // ! prefix
}
```

#### SectionHeader
```csharp
public readonly struct SectionHeader
{
    public Span Span { get; }        // Full "[Name]" span
    public Span ObjectNameSpan { get; } // "Name" span (without brackets)
}
```

#### Kvp (Key-Value Pair)
```csharp
public readonly struct Kvp
{
    public Span Span { get; }           // Full "prop=value" span
    public Span IdentSpan { get; }      // Property name span
    public Span ValueSpan { get; }      // Value span
    public KvpOperation Operation { get; }
}
```

#### Unknown
Lines that don't match any known pattern.

```csharp
public readonly struct Unknown
{
    public Span Span { get; }
    public Span? PreviousSpan { get; } // Previous line (for continuation errors)
}
```

#### Directive (discriminated union)
```csharp
public readonly struct Directive
{
    public DirectiveType Type { get; }
    public SectionHeader? SectionHeader { get; }
    public Kvp? Kvp { get; }
    public Unknown? Unknown { get; }
}

public enum DirectiveType
{
    SectionHeader,
    Kvp,
    Unknown
}
```

### 2.3 Error Types

#### ErrorCode (enum)
```csharp
public enum ErrorCode
{
    InvalidIdent,
    MalformedHeader,
    SpaceAfterMultiline,
    SlashSlashComment,
    BadValue,
    TrailingContinuation,
    StructParseError,
    InvalidStructMember,
    StructDefNotFound,
    Other
}
```

#### Diagnostic
```csharp
public readonly struct Diagnostic
{
    public ErrorCode Code { get; }
    public string Message { get; }
    public SpanWithLocation Location { get; }
    public string SourceLine { get; }
    public DiagnosticSeverity Severity { get; }
    
    public Diagnostic(ErrorCode code, string message, SpanWithLocation location, 
        string sourceLine, DiagnosticSeverity severity = DiagnosticSeverity.Error);
}
```

#### DiagnosticSeverity
```csharp
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}
```

### 2.4 Struct/Array AST

#### Token (lexer output)
```csharp
public enum TokenType
{
    LParen,      // (
    RParen,      // )
    LBrack,      // [
    RBrack,      // ]
    Comma,       // ,
    Eq,          // =
    Semi,        // ;
    Text,        // Unquoted identifier/value
    Quoted       // "string"
}

public readonly struct Token
{
    public TokenType Type { get; }
    public string? Text { get; }  // For Text/Quoted tokens
    public int Position { get; }  // Byte offset
}
```

#### PropValue (AST node)
```csharp
public abstract class PropValue { }

public sealed class TerminalValue : PropValue
{
    public string Text { get; }
    public TerminalValue(string text);
}

public sealed class StructValue : PropValue
{
    public List<PropAssignment> Children { get; }
}

public sealed class ArrayValue : PropValue
{
    public List<PropValue> Elements { get; }
}

public sealed class EmptyValue : PropValue { }

public readonly struct PropAssignment
{
    public string Name { get; }
    public uint? Index { get; }  // For Prop[0] syntax
    public PropValue Value { get; }
}
```

### 2.5 Struct Definition Types

Used by the struct member validator to cache and compare against resolved UnrealScript struct definitions.

#### UnrealStructField
```csharp
public readonly struct UnrealStructField
{
    public string Name { get; }          // Field name as declared in the struct
    public string TypeName { get; }      // Type name (e.g., "name", "int", "float", "array<...>")
    
    public UnrealStructField(string name, string typeName);
}
```

#### UnrealStructDef
```csharp
public sealed class UnrealStructDef
{
    public string StructName { get; }                        // Name of the struct
    public string ClassName { get; }                         // Declaring class name
    public string PackageName { get; }                       // Package the class belongs to
    public string SourceFilePath { get; }                    // Absolute path to the .uc file
    public IReadOnlyList<UnrealStructField> Fields { get; }  // Declared fields
    
    /// Returns the set of field names for quick membership testing.
    public IReadOnlySet<string> FieldNames { get; }
    
    public UnrealStructDef(
        string structName, string className, string packageName,
        string sourceFilePath, IReadOnlyList<UnrealStructField> fields);
}
```

#### StructResolutionResult
```csharp
public readonly struct StructResolutionResult
{
    public bool Found { get; }
    public UnrealStructDef? StructDef { get; }     // Non-null when Found is true
    public string? ResolutionSource { get; }        // Which step resolved it (e.g., "ProjectSrc", "DepMod", "SDK")
    public IReadOnlyList<string> SearchedPaths { get; } // All paths that were searched
    
    public static StructResolutionResult Success(UnrealStructDef def, string source);
    public static StructResolutionResult NotFound(IReadOnlyList<string> searched);
}
```

---

## 3. Component Specifications

### 3.1 Line Splitter

**Input:** Full file content as string  
**Output:** List of `LineSpan` with continuation handling

```csharp
public readonly struct LineSpan
{
    public int Start { get; }
    public int End { get; }
    public bool IsContinuation { get; }  // True if continues from previous
    public bool HasContinuation { get; } // True if continues to next
}

public static class LineSplitter
{
    public static List<LineSpan> Split(string text);
}
```

**Algorithm:**
1. Scan for `\r` or `\n` characters
2. Record line start/end byte offsets
3. For each line, check if it ends with `\\`
4. If `\\` found, mark as continuation and merge with next line
5. Track original line boundaries for error reporting

**Edge Cases:**
- `\r\n` (Windows): treat as single line ending
- `\n` (Unix): treat as line ending
- `\r` (legacy): treat as line ending
- `\\` followed by spaces then newline: mark error, don't continue
- `\\` at EOF: mark error

### 3.2 Directive Tokenizer

**Input:** List of lines with byte spans  
**Output:** List of `Directive` objects

```csharp
public static class DirectiveTokenizer
{
    public static List<Directive> Tokenize(string text, List<LineSpan> lines);
}
```

**Algorithm:**
```
for each line:
    trim leading whitespace
    if line matches "^[.*]$":
        create SectionHeader
    else if line contains '=':
        extract operation prefix (first char if +.!-+)
        extract property name (before '=')
        extract value (after '=')
        handle multi-line continuation
        create Kvp
    else if line is whitespace-only:
        skip (no directive)
    else:
        create Unknown
```

**Regex Patterns:**
```csharp
// Section header: [ObjectName] or [Package.Object]
private static readonly Regex SectionHeaderRegex = 
    new(@"^\[([A-Za-z][A-Za-z0-9_]*(?:[ .][A-Za-z][A-Za-z0-9_]*)?)\]$");

// Property name: MyProp, MyProp[0], MyProp(1)
private static readonly Regex PropertyNameRegex = 
    new(@"^[A-Za-z][A-Za-z0-9_]*(?:\[(?:0|[1-9][0-9]*)\]|\((?:0|[1-9][0-9]*)\))?$");
```

### 3.3 Validator

**Input:** List of `Directive` objects  
**Output:** List of `Diagnostic` objects

```csharp
public interface IValidator
{
    IReadOnlyList<Diagnostic> Validate(string text, List<Directive> directives);
}

public sealed class SimpleSyntaxValidator : IValidator
{
    public IReadOnlyList<Diagnostic> Validate(string text, List<Directive> directives);
}
```

**Validation Rules Implementation:**

#### Section Header Validation
```csharp
private Diagnostic? ValidateSectionHeader(string text, Span span)
{
    // Must match: IDENT or IDENT IDENT
    if (ObjectNameRegex.IsMatch(text))
        return null;
    return new Diagnostic(ErrorCode.InvalidIdent, "Invalid identifier", span, text);
}
```

#### KVP Validation
```csharp
private IReadOnlyList<Diagnostic> ValidateKvp(Kvp kvp, string text)
{
    var errors = new List<Diagnostic>();
    
    // Validate property name
    if (!PropertyNameRegex.IsMatch(kvp.PropertyName))
        errors.Add(InvalidIdent(kvp.IdentSpan));
    
    // Validate value
    errors.AddRange(ValidateValue(kvp.ValueSpan, text));
    
    return errors;
}
```

#### Value Validation
```csharp
private IReadOnlyList<Diagnostic> ValidateValue(Span span, string text)
{
    string value = text.Substring(span.Start, span.Length).Trim();
    
    if (string.IsNullOrEmpty(value))
        return Array.Empty<Diagnostic>();
    
    // Check for trailing continuation
    if (value.EndsWith("\\\\"))
        return new[] { TrailingContinuation(span) };
    
    // Try matching known types
    if (IsBoolean(value)) return Array.Empty<Diagnostic>();
    if (IsNumber(value)) return Array.Empty<Diagnostic>();
    if (IsIdentifier(value)) return Array.Empty<Diagnostic>();
    if (value.StartsWith("("))
        return ValidateStructOrArray(value, span);
    
    return new[] { BadValue(span) };
}
```

#### Struct/Array Validation
```csharp
private IReadOnlyList<Diagnostic> ValidateStructOrArray(string value, Span span)
{
    try
    {
        var result = StructParser.Parse(value);
        return Array.Empty<Diagnostic>();
    }
    catch (ParseException ex)
    {
        return new[]
        {
            new Diagnostic(
                ErrorCode.StructParseError,
                ex.Message,
                new Span(span.Start + ex.Position, span.Start + ex.Position + 1),
                value
            )
        };
    }
}
```

### 3.4 Struct Parser

**Input:** String starting with `(`  
**Output:** `StructValue` or `ArrayValue` AST

```csharp
public static class StructParser
{
    public static PropValue Parse(string text);
}

public sealed class ParseException : Exception
{
    public int Position { get; }
    public ParseException(string message, int position);
}
```

**Lexer Implementation:**
```csharp
private sealed class Lexer
{
    private readonly string _text;
    private int _position;
    
    public Token? Peek();
    public Token Next();
    public void SkipWhitespace();
    
    private Token ReadToken()
    {
        SkipWhitespace();
        
        if (_position >= _text.Length)
            return Token.Eof;
        
        char c = _text[_position];
        return c switch
        {
            '(' => Token.LParen(_position++),
            ')' => Token.RParen(_position++),
            '[' => Token.LBrack(_position++),
            ']' => Token.RBrack(_position++),
            ',' => Token.Comma(_position++),
            '=' => Token.Eq(_position++),
            ';' => Token.Semi(_position++),
            '"' => ReadQuotedString(),
            _ => ReadText()
        };
    }
    
    private Token ReadQuotedString()
    {
        int start = _position++;
        while (_position < _text.Length && _text[_position] != '"')
            _position++;
        if (_position < _text.Length)
            _position++; // closing quote
        return Token.Quoted(_text.Substring(start, _position - start), start);
    }
    
    private Token ReadText()
    {
        int start = _position;
        while (_position < _text.Length)
        {
            char c = _text[_position];
            if (c is ' ' or '\t' or '(' or ')' or '[' or ']' or ',' or '=' or '"' or ';')
                break;
            _position++;
        }
        return Token.Text(_text.Substring(start, _position - start), start);
    }
}
```

**Parser Implementation:**
```csharp
private sealed class Parser
{
    private readonly Lexer _lexer;
    private Token? _peeked;
    
    private Token? Peek() => _peeked ??= _lexer.Peek();
    private Token Next()
    {
        var token = _peeked ?? _lexer.Next();
        _peeked = null;
        return token;
    }
    
    public PropValue Parse()
    {
        if (Next().Type != TokenType.LParen)
            throw new ParseException("Expected '('", 0);
        
        var first = Peek();
        if (first?.Type == TokenType.RParen)
        {
            Next(); // consume )
            return new EmptyValue();
        }
        
        if (first?.Type == TokenType.Text || first?.Type == TokenType.Quoted)
        {
            Next(); // consume first token
            if (Peek()?.Type == TokenType.Eq || Peek()?.Type == TokenType.LBrack)
                return ParseStruct(first);
            else
                return ParseArray(first);
        }
        
        throw new ParseException("Expected property name", _lexer.Position);
    }
    
    private StructValue ParseStruct(Token nameToken)
    {
        var children = new List<PropAssignment>();
        
        while (true)
        {
            // Parse property name
            string name = nameToken.Text!;
            uint? index = ParseOptionalIndex();
            
            // Expect =
            if (Next().Type != TokenType.Eq)
                throw new ParseException("Expected '='", _lexer.Position);
            
            // Parse value
            PropValue value = ParseValue();
            children.Add(new PropAssignment(name, index, value));
            
            // Check for , or )
            var delim = Next();
            if (delim.Type == TokenType.RParen)
                break;
            if (delim.Type != TokenType.Comma)
                throw new ParseException("Expected ',' or ')'", _lexer.Position);
            
            // Get next property name
            nameToken = Next();
            if (nameToken.Type == TokenType.RParen)
                break;
            if (nameToken.Type != TokenType.Text && nameToken.Type != TokenType.Quoted)
                throw new ParseException("Expected property name", _lexer.Position);
        }
        
        return new StructValue { Children = children };
    }
    
    private ArrayValue ParseArray(Token firstToken)
    {
        var elements = new List<PropValue>();
        elements.Add(ToTerminal(firstToken));
        
        while (true)
        {
            var delim = Next();
            if (delim.Type == TokenType.RParen)
                break;
            if (delim.Type != TokenType.Comma)
                throw new ParseException("Expected ',' or ')'", _lexer.Position);
            
            var elem = Next();
            if (elem.Type == TokenType.RParen)
                break;
            elements.Add(ToTerminal(elem));
        }
        
        return new ArrayValue { Elements = elements };
    }
    
    private PropValue ParseValue()
    {
        var token = Next();
        return token.Type switch
        {
            TokenType.Text or TokenType.Quoted => ToTerminal(token),
            TokenType.LParen => ParseStructOrArray(),
            _ => throw new ParseException("Expected value", _lexer.Position)
        };
    }
    
    private PropValue ParseStructOrArray()
    {
        var first = Next();
        if (first.Type == TokenType.RParen)
            return new EmptyValue();
        
        if (first.Type is TokenType.Text or TokenType.Quoted)
        {
            if (Peek()?.Type is TokenType.Eq or TokenType.LBrack)
                return PropValue.Struct(ParseStruct(first));
            else
                return PropValue.Array(ParseArray(first));
        }
        
        throw new ParseException("Expected key-value pair or array value", _lexer.Position);
    }
    
    private uint? ParseOptionalIndex()
    {
        if (Peek()?.Type != TokenType.LBrack)
            return null;
        
        Next(); // consume [
        var indexToken = Next();
        if (indexToken.Type != TokenType.Text || !uint.TryParse(indexToken.Text, out var index))
            throw new ParseException("Expected array index", _lexer.Position);
        
        if (Next().Type != TokenType.RBrack)
            throw new ParseException("Expected ']'", _lexer.Position);
        
        return index;
    }
    
    private static TerminalValue ToTerminal(Token token) =>
        new(token.Text ?? string.Empty);
}
```

### 3.5 Struct Member Validator

**Input:** Parsed directives with struct values, section header context, project root path
**Output:** List of `Diagnostic` objects (errors and warnings)

#### 3.5.1 Overview

The struct member validator operates as a second validation pass after syntax validation. It uses a **two-phase resolution chain**:

1. **Phase 1: Variable Type Resolution** — Find the variable declaration to determine its struct type
2. **Phase 2: Struct Definition Resolution** — Find and recursively map the struct definition

```csharp
public sealed class StructMemberValidator
{
    private readonly VariableTypeResolver _varResolver;
    private readonly StructDefinitionResolver _structResolver;
    private readonly StructCache _cache;
    private readonly bool _enabled;

    public StructMemberValidator(string? projectRoot, string? cacheDir, bool enabled = true)
    {
        _cache = new StructCache(cacheDir ?? ".xcom2cache/structs/");
        _varResolver = new VariableTypeResolver(projectRoot);
        _structResolver = new StructDefinitionResolver(projectRoot, _cache);
        _enabled = enabled;
    }

    public IReadOnlyList<Diagnostic> Validate(
        string text,
        List<Directive> directives,
        string filePath)
    {
        if (!_enabled) return Array.Empty<Diagnostic>();

        var diagnostics = new List<Diagnostic>();
        string? currentSection = null;

        foreach (var directive in directives)
        {
            if (directive.Type == DirectiveType.SectionHeader)
            {
                currentSection = directive.SectionHeader!.Value
                    .ObjectNameSpan.Extract(text);
                continue;
            }

            if (directive.Type != DirectiveType.Kvp || currentSection == null)
                continue;

            var kvp = directive.Kvp!.Value;
            string propertyName = kvp.IdentSpan.Extract(text);
            propertyName = StripIndexSuffix(propertyName);

            PropValue? value = TryParseValue(kvp.ValueSpan, text);
            if (value is not StructValue structValue)
                continue;

            // Phase 1: Get variable type from class definition
            var varTypeResult = _varResolver.Resolve(currentSection, propertyName);
            if (!varTypeResult.Found || string.IsNullOrEmpty(varTypeResult.BaseType))
            {
                diagnostics.Add(CreateStructDefNotFoundWarning(
                    currentSection, propertyName, "Unknown type",
                    kvp.ValueSpan, text, varTypeResult.SearchedPaths));
                continue;
            }

            // Phase 2: Get struct definition (with recursive nested struct resolution)
            var structResult = _structResolver.Resolve(varTypeResult.BaseType);
            if (!structResult.Found || structResult.StructDef == null)
            {
                diagnostics.Add(CreateStructDefNotFoundWarning(
                    currentSection, propertyName, varTypeResult.BaseType,
                    kvp.ValueSpan, text, structResult.SearchedPaths));
                continue;
            }

            // Validate field names
            diagnostics.AddRange(ValidateStructMembers(
                structResult.StructDef, structValue,
                kvp.ValueSpan, text, currentSection, propertyName));
        }

        return diagnostics;
    }
}
```

#### 3.5.2 Phase 1: Variable Type Resolver

Finds the variable declaration in the class file and extracts its type.

```csharp
public sealed class VariableTypeResolver
{
    private readonly ClassFileLocator _locator;
    private readonly UcFileParser _parser;

    public VariableTypeResolver(string? projectRoot)
    {
        _locator = new ClassFileLocator(projectRoot);
        _parser = new UcFileParser();
    }

    public VariableTypeResolutionResult Resolve(string sectionName, string propertyName)
    {
        // Parse section header: [PackageName.ClassName]
        if (!TryParseSectionName(sectionName, out var packageName, out var className))
            return VariableTypeResolutionResult.NotFound(new[] { "Invalid section format" });

        // Locate class file using search chain
        var classFileResult = _locator.Locate(packageName, className);
        if (!classFileResult.Found)
            return VariableTypeResolutionResult.NotFound(classFileResult.SearchedPaths);

        // Parse UC file and find variable declaration
        var classDef = _parser.ParseClass(classFileResult.FilePath!);
        var property = classDef.Properties
            .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

        if (property == null)
            return VariableTypeResolutionResult.NotFound(new[] { $"Property '{propertyName}' not found" });

        // Extract base type (strip array suffix)
        string baseType = GetBaseType(property.TypeName);  // "SDLReplacement[]" → "SDLReplacement"

        return VariableTypeResolutionResult.Success(
            baseType, property.TypeName, classFileResult.FilePath!, classFileResult.SearchedPaths);
    }

    private static string GetBaseType(string typeName)
    {
        // Handle array types: "SDLReplacement[]" → "SDLReplacement"
        int bracketIndex = typeName.IndexOf('[');
        return bracketIndex >= 0 ? typeName.Substring(0, bracketIndex) : typeName;
    }
}

public sealed class VariableTypeResolutionResult
{
    public bool Found { get; }
    public string? BaseType { get; }           // "SDLReplacement"
    public string? FullType { get; }           // "SDLReplacement[]"
    public string? ClassFilePath { get; }      // Where variable was declared
    public IReadOnlyList<string> SearchedPaths { get; }

    public static VariableTypeResolutionResult Success(
        string baseType, string fullType, string classFile, IReadOnlyList<string> searched);
    public static VariableTypeResolutionResult NotFound(IReadOnlyList<string> searched);
}
```

#### 3.5.3 Class File Locator

Locates `.uc` files using the ordered search chain.

```csharp
public sealed class ClassFileLocator
{
    private readonly string? _projectRoot;
    private readonly List<string> _modPaths;
    private readonly string? _sdkPath;

    public ClassFileLocator(string? projectRoot)
    {
        _projectRoot = projectRoot;
        _modPaths = ParseBuildScript(projectRoot);
        _sdkPath = GetSdkPath(projectRoot);
    }

    public ClassFileResult Locate(string packageName, string className)
    {
        var searched = new List<string>();
        string fileName = $"{className}.uc";

        // 1. Project sources
        string projectPath = Path.Combine(_projectRoot ?? "", "Src", packageName, "Classes", fileName);
        if (File.Exists(projectPath))
            return ClassFileResult.Success(projectPath, new[] { "Project" });
        searched.Add(projectPath);

        // 2. Dependent mod sources
        foreach (var modPath in _modPaths)
        {
            string modFilePath = Path.Combine(modPath, packageName, "Classes", fileName);
            if (File.Exists(modFilePath))
                return ClassFileResult.Success(modFilePath, new[] { "DependentMod" });
            searched.Add(modFilePath);
        }

        // 3. Community Highlander
        string communityHL = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".xcom2", "mods", "CommunityHighlander", "Src", packageName, "Classes", fileName);
        if (File.Exists(communityHL))
            return ClassFileResult.Success(communityHL, new[] { "CommunityHighlander" });
        searched.Add(communityHL);

        // 4. Alien Highlander
        string alienHL = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".xcom2", "mods", "AlienHighlander", "Src", packageName, "Classes", fileName);
        if (File.Exists(alienHL))
            return ClassFileResult.Success(alienHL, new[] { "AlienHighlander" });
        searched.Add(alienHL);

        // 5. SDK sources
        if (!string.IsNullOrEmpty(_sdkPath))
        {
            string sdkFile = Path.Combine(_sdkPath, "Development", "SrcOrig", packageName, "Classes", fileName);
            if (File.Exists(sdkFile))
                return ClassFileResult.Success(sdkFile, new[] { "SDK" });
            searched.Add(sdkFile);
        }

        return ClassFileResult.NotFound(searched);
    }
}
```

#### 3.5.4 Phase 2: Struct Definition Resolver with Caching

Finds struct definitions with caching and recursive nested struct resolution.

```csharp
public sealed class StructDefinitionResolver
{
    private readonly StructCache _cache;
    private readonly StructFileLocator _locator;
    private readonly UcFileParser _parser;
    private readonly HashSet<string> _resolving = new();  // Cycle detection

    public StructDefinitionResolver(string? projectRoot, StructCache cache)
    {
        _cache = cache;
        _locator = new StructFileLocator(projectRoot);
        _parser = new UcFileParser();
    }

    public StructResolutionResult Resolve(string structName, int depth = 0)
    {
        // Check cache first
        if (_cache.TryGet(structName, out var cached))
            return StructResolutionResult.Success(cached, "Cache");

        // Cycle detection
        if (_resolving.Contains(structName))
            return StructResolutionResult.Error($"Circular struct reference: {structName}");

        _resolving.Add(structName);
        try
        {
            // Search for struct definition
            var searchResult = _locator.Locate(structName);
            if (!searchResult.Found)
                return StructResolutionResult.NotFound(searchResult.SearchedPaths);

            // Parse struct definition
            var structDef = _parser.ParseStruct(searchResult.FilePath!, structName);
            if (structDef == null)
                return StructResolutionResult.NotFound(searchResult.SearchedPaths);

            // Resolve nested structs (recursive)
            var nestedStructs = new List<string>();
            foreach (var field in structDef.Fields)
            {
                if (IsStructType(field.TypeName) && depth < 10)
                {
                    string nestedTypeName = GetBaseType(field.TypeName);
                    var nestedResult = Resolve(nestedTypeName, depth + 1);
                    if (nestedResult.Found)
                        nestedStructs.Add(nestedTypeName);
                }
            }

            // Create cached definition
            var cachedDef = new CachedStructDef
            {
                StructName = structName,
                SourceFile = searchResult.FilePath!,
                SourceHash = ComputeHash(searchResult.FilePath!),
                LastIndexed = DateTime.UtcNow,
                Fields = structDef.Fields.Select(f => new StructField
                {
                    Name = f.Name,
                    TypeName = f.TypeName,
                    IsStruct = IsStructType(f.TypeName),
                    BaseType = GetBaseType(f.TypeName)
                }).ToList(),
                NestedStructs = nestedStructs,
                ResolvedNestedStructs = true
            };

            // Write to cache
            _cache.Save(cachedDef);

            return StructResolutionResult.Success(cachedDef, searchResult.Source);
        }
        finally
        {
            _resolving.Remove(structName);
        }
    }
}
```

#### 3.5.5 Struct Cache

Persistent JSON cache for resolved struct definitions.

```csharp
public sealed class StructCache
{
    private readonly string _cacheDir;
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(24);

    public StructCache(string cacheDir)
    {
        _cacheDir = cacheDir;
        Directory.CreateDirectory(cacheDir);
    }

    public bool TryGet(string structName, out CachedStructDef? cached)
    {
        string cacheFile = GetCachePath(structName);
        if (!File.Exists(cacheFile))
        {
            cached = null;
            return false;
        }

        var cache = JsonSerializer.Deserialize<CachedStructDef>(File.ReadAllText(cacheFile));
        if (cache == null)
        {
            cached = null;
            return false;
        }

        // Validate cache
        if (DateTime.UtcNow - cache.LastIndexed > _maxAge)
        {
            cached = null;
            return false;
        }

        if (!File.Exists(cache.SourceFile))
        {
            cached = null;
            return false;
        }

        string currentHash = ComputeHash(cache.SourceFile);
        if (currentHash != cache.SourceHash)
        {
            cached = null;
            return false;
        }

        cached = cache;
        return true;
    }

    public void Save(CachedStructDef def)
    {
        string cacheFile = GetCachePath(def.StructName);
        File.WriteAllText(cacheFile, JsonSerializer.Serialize(def, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private string GetCachePath(string structName) =>
        Path.Combine(_cacheDir, $"{structName}.json");
}

public sealed class CachedStructDef
{
    public string StructName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public DateTime LastIndexed { get; set; }
    public List<StructField> Fields { get; set; } = new();
    public List<string> NestedStructs { get; set; } = new();
    public bool ResolvedNestedStructs { get; set; }
}

public sealed class StructField
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public bool IsStruct { get; set; }
    public string BaseType { get; set; } = "";
}
```

#### 3.5.6 Search Chain Summary

| Phase | Step | Source | Search Pattern |
|-------|------|--------|----------------|
| **1. Variable Type** | 1 | Project sources | `Src/<Package>/Classes/<Class>.uc` |
| | 2 | Dependent mods | `<mod-path>/<Package>/Classes/<Class>.uc` |
| | 3 | Community Highlander | `~/.xcom2/mods/CommunityHighlander/Src/...` |
| | 4 | Alien Highlander | `~/.xcom2/mods/AlienHighlander/Src/...` |
| | 5 | SDK sources | `<sdkroot>/Development/SrcOrig/...` |
| **2. Struct Def** | 1 | Local cache | `.xcom2cache/structs/<StructName>.json` |
| | 2 | Project sources | Full scan: `Src/**/*.uc` |
| | 3 | Dependent mods | Full scan in each mod path |
| | 4 | Community Highlander | Full scan |
| | 5 | Alien Highlander | Full scan |
| | 6 | SDK sources | Full scan in `SrcOrig/` |
    
    structDef = structDefResult;
    return true;
}
```

#### 3.5.3 UnrealScript (.uc) File Parser

Parses UnrealScript source files to extract struct definitions and config property declarations. This is **not** a full UnrealScript compiler — it extracts only the information needed for struct member validation.

```csharp
public sealed class UcFileParser
{
    /// <summary>
    /// Parse a .uc file and extract struct definitions and config property declarations.
    /// </summary>
    public static UcClassDef Parse(string filePath);
}

public sealed class UcClassDef
{
    public string ClassName { get; }
    public string PackageName { get; }
    public string FilePath { get; }
    public IReadOnlyList<UnrealStructDef> Structs { get; }
    public IReadOnlyList<UcPropertyDef> Properties { get; }
}

public sealed class UcPropertyDef
{
    public string Name { get; }            // e.g., "SDLReplacements"
    public string TypeName { get; }        // e.g., "array" or "SpawnDistributionListReplacement"
    public string? ElementTypeName { get; } // For array types: the element type name
    public bool IsConfig { get; }          // Whether declared with "config" keyword
}
```

**Parsing Algorithm:**

The UC parser uses regex-based line scanning (not a full parser) to extract the relevant declarations:

```csharp
private static readonly Regex StructStartRegex = new(
    @"^\s*struct\s+(\w+)\b", RegexOptions.IgnoreCase);

private static readonly Regex StructFieldRegex = new(
    @"^\s*var(?:\s*\([^)]*\))?\s+(?:config\s+)?" +
    @"(?:array\s*<\s*(\w+)\s*>|(\w+))\s+(\w+)\s*;",
    RegexOptions.IgnoreCase);

private static readonly Regex PropertyRegex = new(
    @"^\s*var(?:\s*\([^)]*\))?\s+(?:config)\s+" +
    @"(?:array\s*<\s*(\w+)\s*>|(\w+))\s+(\w+)\s*;",
    RegexOptions.IgnoreCase);
```

The parser scans the file line-by-line:

1. **Detect struct start:** Match `struct StructName` (optionally `extends ...`). Record the struct name and begin collecting fields.
2. **Detect struct fields:** Inside a struct block, match `var [specifiers] Type FieldName;` declarations. Extract the field name and type. For `array<ElementType>` fields, extract the element type name.
3. **Detect struct end:** Match the closing `};` or `structdefaultproperties` block (which precedes `};`).
4. **Detect config properties:** Outside struct blocks, match `var config ...` declarations to identify which properties are config-accessible and their types.

**Example input (`ELPSDLProcessor.uc`):**
```unrealscript
struct SpawnDistributionListReplacement {
    var name List;
    var eDuplicates Duplicates;
    var array<GroupEntryStruct> Groups;
    var float CommonWeightMultiplier;
    var int CommonMinOffset;
    var int CommonMaxOffset;
    var array<SpawnDistributionListEntry> Extras;
    var array<name> Purge;
    ...
};

var config array<SpawnDistributionListReplacement> SDLReplacements;
```

**Extracted result:**
- `StructDef("SpawnDistributionListReplacement")` with fields: `List`, `Duplicates`, `Groups`, `CommonWeightMultiplier`, `CommonMinOffset`, `CommonMaxOffset`, `Extras`, `Purge`
- `PropertyDef("SDLReplacements", type="array", elementType="SpawnDistributionListReplacement", isConfig=true)`

**Parser Limitations:**

The UC parser uses regex-based line scanning and is **best-effort**, not a full UnrealScript compiler. The following cases may not be handled correctly:

| Limitation | Example | Behavior |
|------------|---------|----------|
| Multi-line type declarations | `var array<\n    MyType\n> MyProp;` | May fail to parse |
| Nested struct definitions | `struct Outer { struct Inner { ... }; }` | Only outer struct detected |
| Comments within declarations | `var // comment\n    int MyProp;` | May fail to parse |
| Complex specifiers | `var editconst array<Type> Prop;` | May not recognize all specifiers |
| Inherited properties | Properties from parent classes | Not resolved (future enhancement) |

For typical UE3 config-related structs, the parser handles >95% of cases. Edge cases can be addressed in future versions with a full tokenizer-based parser.

#### 3.5.4 Build Script Parser

Parses `.scripts/build.ps1` to extract dependent mod source paths.

```csharp
public static class BuildScriptParser
{
    private static readonly Regex IncludeSrcRegex = new(
        @"^\s*\$builder\.IncludeSrc\(\s*""(.+?)""\s*\)",
        RegexOptions.Multiline);
    
    /// <summary>
    /// Parse a build.ps1 file and extract all uncommented IncludeSrc paths.
    /// Commented lines (starting with # after optional whitespace) are skipped.
    /// </summary>
    public static IReadOnlyList<string> ParseIncludeSrcPaths(string filePath)
    {
        var paths = new List<string>();
        
        foreach (string line in File.ReadLines(filePath))
        {
            string trimmed = line.TrimStart();
            
            // Skip commented-out lines
            if (trimmed.StartsWith("#"))
                continue;
            
            var match = IncludeSrcRegex.Match(line);
            if (match.Success)
            {
                string path = match.Groups[1].Value;
                // Resolve PowerShell variables if needed
                path = ResolveSimpleVariables(path, filePath);
                paths.Add(path);
            }
        }
        
        return paths;
    }
}
```

**Example input (`.scripts/build.ps1`):**
```powershell
# Commented out: $builder.IncludeSrc("...") ← skipped
$builder.IncludeSrc("E:\Games\Steam\steamapps\workshop\content\268500\1134256495\Src")
$builder.IncludeSrc("E:\Games\Steam\steamapps\workshop\content\268500\2534737016\Src")
```

**Extracted result:** Two paths pointing to Workshop mod source directories.

#### 3.5.5 Settings File Parser

Reads `.vscode/settings.json` to extract the SDK root path.

```csharp
public static class VsCodeSettingsParser
{
    /// <summary>
    /// Extract the xcom.highlander.sdkroot value from .vscode/settings.json.
    /// Returns null if the file or key does not exist.
    /// </summary>
    public static string? GetSdkRoot(string projectRoot)
    {
        string settingsPath = Path.Combine(projectRoot, ".vscode", "settings.json");
        if (!File.Exists(settingsPath))
            return null;
        
        using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
        if (doc.RootElement.TryGetProperty("xcom.highlander.sdkroot", out var value))
            return value.GetString();
        
        return null;
    }
}
```

**Example input (`.vscode/settings.json`):**
```json
{
    "xcom.highlander.sdkroot": "E:\\Games\\Steam\\...\\XCOM 2 War of the Chosen SDK"
}
```

**Resolved SDK source path:** `E:\Games\Steam\...\XCOM 2 War of the Chosen SDK\Development\SrcOrig\`

#### 3.5.6 Property-to-Struct Type Resolution

Not all config properties use struct types. The validator must determine whether a property's type is a struct before attempting validation.

**Type Classification:**

| Property Type | Example | Validation Behavior |
|---------------|---------|---------------------|
| Struct type | `var config MyStruct Prop;` | Validate struct members |
| Array of structs | `var config array<MyStruct> Prop;` | Validate struct members |
| Primitive type | `var config int Prop;` | Skip validation |
| Enum type | `var config EMyEnum Prop;` | Skip validation |
| Class reference | `var config MyClass Prop;` | Skip validation |
| Name/String | `var config name Prop;` | Skip validation |

**Resolution Logic:**

```csharp
private static bool TryGetStructTypeName(
    UcPropertyDef propertyDef, out string? structTypeName)
{
    // For array<T>, extract T
    string typeName = propertyDef.ElementTypeName ?? propertyDef.TypeName;
    
    // Skip primitive types
    if (IsPrimitiveType(typeName))
    {
        structTypeName = null;
        return false;
    }
    
    // Skip enum types (convention: starts with E)
    if (typeName.StartsWith("E") && char.IsUpper(typeName[1]))
    {
        structTypeName = null;
        return false;
    }
    
    // Skip class references (convention: ends with "Ref" or known class names)
    if (IsClassType(typeName))
    {
        structTypeName = null;
        return false;
    }
    
    // Assume it's a struct type
    structTypeName = typeName;
    return true;
}

private static bool IsPrimitiveType(string typeName)
{
    return typeName.ToLowerInvariant() switch
    {
        "int" or "float" or "bool" or "byte" or "string" or "name" => true,
        _ => false
    };
}
```

**Note:** If a property is not found in the class definition, struct validation is skipped (no error or warning). This handles cases where the config file references properties that don't exist in the source (which would be caught by other validation rules).

#### 3.5.7 Validation Logic

```csharp
private IReadOnlyList<Diagnostic> ValidateStructMembers(
    string sectionName, string propertyName, StructValue structValue,
    Span kvpSpan, string text, string filePath)
{
    var result = _resolver.Resolve(sectionName, propertyName);
    var diagnostics = new List<Diagnostic>();
    
    if (!result.Found)
    {
        // Emit warning, not error
        diagnostics.Add(new Diagnostic(
            ErrorCode.StructDefNotFound,
            $"Could not resolve struct definition for [{sectionName}] " +
            $"property \"{propertyName}\". Struct member validation skipped. " +
            $"Searched: {string.Join(", ", result.SearchedPaths)}",
            kvpSpan,
            text,
            DiagnosticSeverity.Warning));
        return diagnostics;
    }
    
    var structDef = result.StructDef!;
    
    foreach (var child in structValue.Children)
    {
        string fieldName = child.Name;
        
        // Strip array index suffix for lookup (e.g., "Groups" from "Groups[0]")
        if (!structDef.FieldNames.Contains(fieldName))
        {
            diagnostics.Add(new Diagnostic(
                ErrorCode.InvalidStructMember,
                $"Unknown struct member \"{fieldName}\" in [{sectionName}] " +
                $"property \"{propertyName}\". " +
                $"Valid members: {string.Join(", ", structDef.FieldNames)}. " +
                $"Struct defined in: {structDef.SourceFilePath}",
                kvpSpan,
                text,
                DiagnosticSeverity.Error));
        }
    }
    
    return diagnostics;
}
```

#### 3.5.8 Caching Strategy

Struct definition resolution is cached at three levels to avoid redundant file I/O and parsing:

| Level | Cache Key | Contents | Lifetime |
|-------|-----------|----------|----------|
| Class-level | `PackageName.ClassName` | Parsed `UcClassDef` with all structs and properties | Per tool invocation |
| Struct-type-level | `PackageName.ClassName::StructTypeName` | `UnrealStructDef` extracted from class | Per tool invocation |
| Resolution-level | `PackageName.ClassName::PropertyName` | `StructResolutionResult` | Per tool invocation |

**Struct-Type-Level Cache:**

When multiple properties reference the same struct type, the struct definition is cached by type name to avoid re-extraction:

```csharp
public sealed class StructDefinitionResolver
{
    private readonly Dictionary<string, UnrealStructDef?> _structTypeCache = new();
    
    private UnrealStructDef? GetStructDefByType(
        string packageName, string className, string structTypeName)
    {
        string cacheKey = $"{packageName}.{className}::{structTypeName}";
        
        if (_structTypeCache.TryGetValue(cacheKey, out var cached))
            return cached;
        
        // Resolve class definition
        var classDef = GetClassDef(packageName, className);
        if (classDef == null)
        {
            _structTypeCache[cacheKey] = null;
            return null;
        }
        
        // Extract struct definition
        var structDef = classDef.Structs
            .FirstOrDefault(s => s.StructName.Equals(structTypeName, 
                StringComparison.OrdinalIgnoreCase));
        
        _structTypeCache[cacheKey] = structDef;
        return structDef;
    }
}
```

This ensures that:
- Each `.uc` file is parsed at most once, even if multiple config entries reference structs from the same class.
- Each struct type is extracted at most once, even if referenced by multiple properties.
- The build script and settings file are parsed at most once (lazily, on first need).
- Repeated references to the same struct property across multiple config files reuse the cached result.

### 3.6 File System Walker

```csharp
public sealed class FileSystemWalker
{
    private readonly string _pattern;
    private readonly bool _recursive;
    
    public IEnumerable<string> GetFiles(string path)
    {
        if (File.Exists(path))
        {
            yield return path;
            yield break;
        }
        
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = _recursive,
            IgnoreInaccessible = true
        };
        
        foreach (var file in Directory.EnumerateFiles(path, _pattern, options))
            yield return file;
    }
}
```

### 3.7 File Reader

```csharp
public sealed class FileReader
{
    public static ReadResult Read(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            
            // Strip BOM if present
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                bytes = bytes.Skip(3).ToArray();
            
            // Validate UTF-8
            try
            {
                string content = Encoding.UTF8.GetString(bytes);
                // Validate by round-trip
                Encoding.UTF8.GetBytes(content);
                return ReadResult.Success(content);
            }
            catch (ArgumentException)
            {
                return ReadResult.InvalidUtf8(path);
            }
        }
        catch (IOException ex)
        {
            return ReadResult.IOError(path, ex.Message);
        }
    }
}

public readonly struct ReadResult
{
    public bool Success { get; }
    public string? Content { get; }
    public string? ErrorPath { get; }
    public string? ErrorMessage { get; }
    public ReadErrorType ErrorType { get; }
    
    public static ReadResult Success(string content) => 
        new() { Success = true, Content = content };
    
    public static ReadResult InvalidUtf8(string path) =>
        new() { Success = false, ErrorPath = path, ErrorType = ReadErrorType.InvalidUtf8 };
    
    public static ReadResult IOError(string path, string message) =>
        new() { Success = false, ErrorPath = path, ErrorMessage = message, ErrorType = ReadErrorType.IO };
}

public enum ReadErrorType
{
    None,
    InvalidUtf8,
    IO
}
```

### 3.8 Line/Column Lookup

For converting byte offsets to line/column positions:

```csharp
public sealed class LineColLookup
{
    private readonly int[] _lineStarts;
    
    public LineColLookup(string text)
    {
        var lines = new List<int> { 0 };
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\r' || text[i] == '\n')
            {
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                lines.Add(i + 1);
            }
            i++;
        }
        _lineStarts = lines.ToArray();
    }
    
    public (int line, int col) Get(int byteOffset)
    {
        // Binary search for line
        int line = _lineStarts.Length - 1;
        for (int i = 0; i < _lineStarts.Length; i++)
        {
            if (_lineStarts[i] <= byteOffset)
                line = i + 1; // 1-based
            else
                break;
        }
        
        int col = byteOffset - _lineStarts[line - 1] + 1; // 1-based
        return (line, col);
    }
}
```

---

## 4. Output Formatting

### 4.1 Console Formatter

```csharp
public sealed class ConsoleFormatter
{
    public void Write(Diagnostic diagnostic)
    {
        var loc = diagnostic.Location;
        
        // File(line,col-endline,endcol): error code: message
        Console.Write($"{diagnostic.FilePath}({loc.Start.Line},{loc.Start.Column}");
        if (loc.End.Line != loc.Start.Line || loc.End.Column != loc.Start.Column)
            Console.Write($"-{loc.End.Line},{loc.End.Column}");
        Console.WriteLine($"): {diagnostic.Code}: {diagnostic.Message}");
        
        // Source line excerpt
        Console.WriteLine($"  {diagnostic.SourceLine}");
    }
}
```

### 4.2 JSON Formatter

```csharp
public sealed class JsonFormatter
{
    public string Format(IReadOnlyList<FileResult> results)
    {
        var output = new
        {
            files = results.Select(f => new
            {
                path = f.Path,
                errors = f.Diagnostics.Select(d => new
                {
                    code = d.Code.ToString(),
                    message = d.Message,
                    line = d.Location.Start.Line,
                    column = d.Location.Start.Column,
                    endLine = d.Location.End.Line,
                    endColumn = d.Location.End.Column,
                    sourceLine = d.SourceLine
                })
            }),
            summary = new
            {
                filesProcessed = results.Count,
                filesWithErrors = results.Count(r => r.Diagnostics.Count > 0),
                totalErrors = results.Sum(r => r.Diagnostics.Count)
            }
        };
        
        return JsonSerializer.Serialize(output, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
}
```

---

## 5. Error Handling Strategy

### 5.1 Error Categories

| Category | Handling |
|----------|----------|
| Parse errors | Collect and continue |
| I/O errors | Log to stderr, skip file, continue |
| Invalid UTF-8 | Report, skip file, continue |
| Invalid arguments | Show help, exit code 4 |

### 5.2 Error Recovery

```csharp
public sealed class ErrorCollector
{
    private readonly List<Diagnostic> _diagnostics = new();
    
    public void Add(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);
    
    public void AddRange(IEnumerable<Diagnostic> diagnostics) => 
        _diagnostics.AddRange(diagnostics);
    
    public IReadOnlyList<Diagnostic> GetAll() => _diagnostics.AsReadOnly();
    
    public bool HasErrors => _diagnostics.Count > 0;
}
```

### 5.3 Exception Boundaries

```csharp
// In file processing loop
try
{
    var result = ProcessFile(path);
    results.Add(result);
}
catch (IOException ex)
{
    Console.Error.WriteLine($"Error reading {path}: {ex.Message}");
    skippedFiles++;
}
catch (UnauthorizedAccessException ex)
{
    Console.Error.WriteLine($"Access denied: {path}");
    skippedFiles++;
}
```

---

## 6. Performance Considerations

### 6.1 Memory Management

- Use `string.Substring` sparingly; prefer `Span<char>` for parsing
- Reuse buffers where possible
- Stream large files if memory becomes constraint

### 6.2 Parallel Processing

```csharp
// For batch processing multiple files
var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

await Parallel.ForEachAsync(files, options, async (path, token) =>
{
    var result = await ProcessFileAsync(path);
    results.Add(result);
});
```

### 6.3 String Interning

- Don't intern property names (too many unique values)
- Consider interning error codes (limited set)

---

## 7. Testing Strategy

### 7.1 Unit Tests

| Component | Test Focus |
|-----------|------------|
| LineSplitter | Line boundaries, continuation handling |
| DirectiveTokenizer | Correct directive classification |
| Validator | All error types triggered correctly |
| StructParser | Valid/invalid struct syntax |
| LineColLookup | Accurate position conversion |
| UcFileParser | Struct and property extraction from .uc files |
| BuildScriptParser | IncludeSrc path extraction, comment skipping |
| VsCodeSettingsParser | SDK root extraction |
| StructDefinitionResolver | Four-step resolution chain, caching |
| StructMemberValidator | Field name validation, error/warning diagnostics |

### 7.2 Test Data

```
test_data/
├── valid/
│   ├── simple.ini
│   ├── multiline.ini
│   └── complex_structs.ini
├── invalid/
│   ├── bad_header.ini
│   ├── bad_property.ini
│   ├── bad_value.ini
│   └── trailing_continuation.ini
├── struct_validation/
│   ├── valid_struct_members.ini
│   ├── invalid_struct_members.ini
│   ├── unresolvable_struct.ini
│   ├── mock_project/
│   │   ├── Src/
│   │   │   └── TestPackage/
│   │   │       └── Classes/
│   │   │           └── TestClass.uc
│   │   ├── .scripts/
│   │   │   └── build.ps1
│   │   └── .vscode/
│   │       └── settings.json
│   └── mock_uc_files/
│       ├── simple_struct.uc
│       ├── nested_struct.uc
│       └── array_of_structs.uc
└── edge_cases/
    ├── empty.ini
    ├── whitespace_only.ini
    ├── comments_only.ini
    └── utf8_bom.ini
```

### 7.3 Expected Output Files

For each test input, maintain expected output:

```
test_data/invalid/bad_header.ini
test_data/invalid/bad_header.errors.json  (expected diagnostics)
```

### 7.4 Reference Test Cases

Port all test cases from Rust implementation:
- `buggy_section_header`
- `buggy_backslashes`
- `correct_section_header`
- `curly`
- `what`
- `test_ok_tokens`
- `test_small`
- `test_semi`
- `exciting`
- `trailing`

---

## 8. Build & Deployment

### 8.1 Project Structure

```
ue3-config-parser-cli/
├── src/
│   ├── Program.cs           # Entry point
│   ├── Cli/
│   │   ├── Options.cs       # CLI options model
│   │   └── HelpText.cs      # Help message
│   ├── IO/
│   │   ├── FileSystemWalker.cs
│   │   └── FileReader.cs
│   ├── Parsing/
│   │   ├── LineSplitter.cs
│   │   ├── DirectiveTokenizer.cs
│   │   ├── Span.cs
│   │   └── Directive.cs
│   ├── Validation/
│   │   ├── IValidator.cs
│   │   ├── SimpleSyntaxValidator.cs
│   │   ├── Diagnostic.cs
│   │   └── ErrorCodes.cs
│   ├── StructSyntax/
│   │   ├── Lexer.cs
│   │   ├── Parser.cs
│   │   ├── Tokens.cs
│   │   └── Ast.cs
│   ├── StructValidation/
│   │   ├── StructMemberValidator.cs
│   │   ├── StructDefinitionResolver.cs
│   │   ├── UcFileParser.cs
│   │   ├── BuildScriptParser.cs
│   │   ├── VsCodeSettingsParser.cs
│   │   └── Models.cs            # UnrealStructDef, UcClassDef, etc.
│   └── Output/
│       ├── ConsoleFormatter.cs
│       ├── JsonFormatter.cs
│       └── LineColLookup.cs
├── tests/
│   ├── UnitTests/
│   └── IntegrationTests/
├── test_data/
├── ue3-config-parser-cli.csproj
└── README.md
```

### 8.2 .csproj Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishTrimmed>true</PublishTrimmed>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
  </ItemGroup>
</Project>
```

### 8.3 Build Commands

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Publish single-file executable
dotnet publish -c Release -r win-x64 --self-contained

# Run tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

---

## 9. Directory Structure (Final)

```
D:\Downloads\ue3-config-parser-main\
├── PRD.md                    # This PRD
├── SYSTEM_DESIGN.md          # This design doc
├── ue3-config-parser/        # Reference Rust implementation
├── wasm-ue3-config-parser/   # Reference WASM implementation
└── ue3-config-parser-cli/    # New C# implementation
    ├── src/
    ├── tests/
    ├── test_data/
    └── ...
```

---

## 10. Implementation Checklist

### Phase 1: Core Parser
- [ ] Span and SourceLocation types
- [ ] LineSplitter with continuation handling
- [ ] DirectiveTokenizer
- [ ] Basic Validator (section headers, KVPs)

### Phase 2: Struct Syntax
- [ ] Lexer for struct/array tokens
- [ ] Recursive descent parser
- [ ] Error reporting with positions

### Phase 3: CLI
- [ ] Command-line argument parsing
- [ ] File system walker
- [ ] File reader with UTF-8 validation
- [ ] Console output formatter

### Phase 4: Advanced Features
- [ ] JSON output format
- [ ] Summary mode
- [ ] Glob pattern support
- [ ] Exit codes

### Phase 5: Struct Member Validation
- [ ] UnrealScript (.uc) file parser (struct definitions + config properties)
- [ ] Build script parser (`.scripts/build.ps1` → `IncludeSrc` paths)
- [ ] VS Code settings parser (`.vscode/settings.json` → SDK root)
- [ ] Struct definition resolver (four-step resolution chain)
- [ ] Resolution cache (class-level, struct-type-level, and resolution-level)
- [ ] Property-to-struct type resolution (skip primitives, enums, classes)
- [ ] Struct member validator (field name comparison + diagnostics)
- [ ] `--project-root` and `--no-struct-validation` CLI options
- [ ] `InvalidStructMember` error and `StructDefNotFound` warning output formatting
- [ ] UC parser limitations documentation (regex-based, best-effort)

### Phase 6: Testing
- [ ] Unit tests for all components
- [ ] Port Rust test cases
- [ ] UC parser tests (struct extraction, property extraction)
- [ ] Build script and settings parser tests
- [ ] Struct resolution chain integration tests
- [ ] Struct member validation end-to-end tests
- [ ] Integration tests
- [ ] Performance benchmarks

### Phase 7: Polish
- [ ] Help text
- [ ] Error message improvements
- [ ] Documentation
- [ ] Release build configuration
