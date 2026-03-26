namespace X2ModCompiler.Core.Parser;

/// <summary>
/// Provides a high-level API for parsing Unreal Engine 3 (XCOM 2) configuration structure and array syntax.
/// This parser handles the specialized '(...)' grammar used for complex property values in .ini files.
/// </summary>
public static class StructParser
{
    /// <summary>
    /// Parses a struct or array value from text, assuming the content starts with an opening parenthesis '('.
    /// </summary>
    /// <param name="text">The raw string value to parse.</param>
    /// <returns>A <see cref="PropValue"/> representing the parsed structure or array.</returns>
    /// <exception cref="ParseException">Thrown if the text does not conform to the expected grammar.</exception>
    public static PropValue Parse(string text)
    {
        var lexer = new Lexer(text);
        var parser = new Parser(text, lexer);
        return parser.Parse();
    }

    /// <summary>
    /// Attempts to parse a struct or array value. Returns null if the value does not appear to be a structure
    /// or if a parsing error occurs.
    /// </summary>
    /// <param name="text">The raw string value to check and parse.</param>
    /// <returns>A <see cref="PropValue"/> if parsing was successful; otherwise null.</returns>
    public static PropValue? TryParse(string text)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.Length == 0 || trimmed[0] != '(')
            return null;

        try
        {
            var lexer = new Lexer(trimmed);
            var parser = new Parser(trimmed, lexer);
            return parser.Parse();
        }
        catch (ParseException)
        {
            return null;
        }
    }
}

/// <summary>
/// Internal recursive descent parser for the UE3 configuration grammar.
/// </summary>
/// <summary>
/// Internal recursive descent parser for the UE3 configuration grammar.
/// </summary>
file ref struct Parser
{
    private Lexer _lexer;
    private readonly ReadOnlySpan<char> _source;
    private Token? _peeked;

    /// <summary>
    /// Initializes a new instance of the <see cref="Parser"/> class.
    /// </summary>
    /// <param name="source">The source text being parsed.</param>
    /// <param name="lexer">The lexer providing the token stream.</param>
    public Parser(ReadOnlySpan<char> source, Lexer lexer)
    {
        _source = source;
        _lexer = lexer;
        _peeked = null;
    }

    private Token? Peek() => _peeked ??= _lexer.Next();

    private Token Next()
    {
        var token = _peeked ?? _lexer.Next();
        _peeked = null;
        return token;
    }

    /// <summary>
    /// Entry point for the recursive descent process. Parses a single parenthetical value.
    /// </summary>
    /// <returns>A parsed <see cref="PropValue"/>.</returns>
    public PropValue Parse()
    {
        if (Next().Type != TokenType.LParen)
            throw new ParseException("Expected '('", 0);

        var firstToken = Peek();
        if (firstToken?.Type == TokenType.RParen)
        {
            Next(); // consume )
            return new EmptyValue();
        }

        if (firstToken?.Type == TokenType.Text || firstToken?.Type == TokenType.Quoted)
        {
            var nameToken = Next(); // consume first token
            var nextToken = Peek();
            if (nextToken?.Type == TokenType.Eq || nextToken?.Type == TokenType.LBrack)
                return ParseStruct(nameToken);
            else
                return ParseArray(nameToken);
        }

        // Handle array of structs: ((A=1), (A=2))
        if (firstToken?.Type == TokenType.LParen)
        {
            // This is an array where elements are structs
            return ParseArray(new Token(TokenType.LParen, new Range(_lexer.Position, _lexer.Position + 1), _lexer.Position));
        }

        throw new ParseException("Expected property name or value", _lexer.Position);
    }

    /// <summary>
    /// Parses a named data structure (struct) starting with a property name.
    /// Example: (PropertyA=ValueA, PropertyB=ValueB)
    /// </summary>
    private StructValue ParseStruct(Token nameToken)
    {
        var children = new List<PropAssignment>();
        var currentNameToken = nameToken;

        while (true)
        {
            // Parse property name
            string name = GetTokenValue(currentNameToken);
            string? index = ParseOptionalIndex();

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
            var nextToken = Next();
            if (nextToken.Type == TokenType.RParen)
                break;
            if (nextToken.Type != TokenType.Text && nextToken.Type != TokenType.Quoted)
                throw new ParseException("Expected property name", _lexer.Position);
            
            currentNameToken = nextToken;
        }

        return new StructValue(children);
    }

    /// <summary>
    /// Parses a sequence of comma-separated values (array).
    /// Example: (Value1, Value2, Value3)
    /// </summary>
    private ArrayValue ParseArray(Token firstToken)
    {
        var elements = new List<PropValue>();
        
        // Check if first element is a struct (starts with LParen)
        if (firstToken.Type == TokenType.LParen)
        {
            // Array of structs
            _peeked = firstToken;
            elements.Add(Parse());
        }
        else
        {
            elements.Add(ToTerminal(firstToken));
        }

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
            if (elem.Type == TokenType.LParen)
            {
                // Nested struct in array
                _peeked = elem;
                elements.Add(Parse());
            }
            else
            {
                elements.Add(ToTerminal(elem));
            }
        }

        return new ArrayValue(elements);
    }

    /// <summary>
    /// Parses a generic property value, which could be a terminal value, a nested struct, or a nested array.
    /// </summary>
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

    /// <summary>
    /// Disambiguates between a struct or an array value by peeking at the next token.
    /// </summary>
    private PropValue ParseStructOrArray()
    {
        var first = Next();
        if (first.Type == TokenType.RParen)
            return new EmptyValue();

        if (first.Type is TokenType.Text or TokenType.Quoted)
        {
            var nextToken = Peek();
            if (nextToken?.Type is TokenType.Eq or TokenType.LBrack)
                return ParseStruct(first);
            else
                return ParseArray(first);
        }

        throw new ParseException("Expected key-value pair or array value", _lexer.Position);
    }

    /// <summary>
    /// Parses an optional array index suffix for property names, such as Property[0].
    /// </summary>
    private string? ParseOptionalIndex()
    {
        if (Peek()?.Type != TokenType.LBrack)
            return null;

        Next(); // consume [
        var indexToken = Next();
        if (indexToken.Type != TokenType.Text && indexToken.Type != TokenType.Quoted)
            throw new ParseException("Expected array index", _lexer.Position);

        string index = GetTokenValue(indexToken);

        if (Next().Type != TokenType.RBrack)
            throw new ParseException("Expected ']'", _lexer.Position);

        return index;
    }

    private string GetTokenValue(Token token) 
    {
        return _source[token.Range].ToString();
    }

    private TerminalValue ToTerminal(Token token) =>
        new(GetTokenValue(token));
}
