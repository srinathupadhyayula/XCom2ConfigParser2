namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Parser for UE3 struct and array syntax.
/// </summary>
public static class StructParser
{
    /// <summary>
    /// Parses a struct or array value from text starting with '('.
    /// </summary>
    public static PropValue Parse(string text)
    {
        var lexer = new Lexer(text);
        var parser = new Parser(lexer);
        return parser.Parse();
    }

    /// <summary>
    /// Tries to parse a struct or array value, returning null if it's not a struct/array.
    /// </summary>
    public static PropValue? TryParse(string text)
    {
        string trimmed = text.Trim();
        if (!trimmed.StartsWith("("))
            return null;

        try
        {
            return Parse(trimmed);
        }
        catch (ParseException)
        {
            return null;
        }
    }
}

/// <summary>
/// Internal parser implementation.
/// </summary>
file sealed class Parser
{
    private readonly Lexer _lexer;
    private Token? _peeked;

    public Parser(Lexer lexer)
    {
        _lexer = lexer;
    }

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
            var nameToken = Next(); // consume first token
            if (Peek()?.Type == TokenType.Eq || Peek()?.Type == TokenType.LBrack)
                return ParseStruct(nameToken);
            else
                return ParseArray(nameToken);
        }

        // Handle array of structs: ((A=1), (A=2))
        if (first?.Type == TokenType.LParen)
        {
            // This is an array where elements are structs
            return ParseArray(new Token(TokenType.LParen, null, _lexer.Position));
        }

        throw new ParseException("Expected property name or value", _lexer.Position);
    }

    private StructValue ParseStruct(Token nameToken)
    {
        var children = new List<PropAssignment>();
        var currentNameToken = nameToken;

        while (true)
        {
            // Parse property name
            string name = currentNameToken.Value ?? string.Empty;
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
            var nextToken = Next();
            if (nextToken.Type == TokenType.RParen)
                break;
            if (nextToken.Type != TokenType.Text && nextToken.Type != TokenType.Quoted)
                throw new ParseException("Expected property name", _lexer.Position);
            
            currentNameToken = nextToken;
        }

        return new StructValue(children);
    }

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
                return ParseStruct(first);
            else
                return ParseArray(first);
        }

        throw new ParseException("Expected key-value pair or array value", _lexer.Position);
    }

    private uint? ParseOptionalIndex()
    {
        if (Peek()?.Type != TokenType.LBrack)
            return null;

        Next(); // consume [
        var indexToken = Next();
        if (indexToken.Type != TokenType.Text || !uint.TryParse(indexToken.Value, out var index))
            throw new ParseException("Expected array index", _lexer.Position);

        if (Next().Type != TokenType.RBrack)
            throw new ParseException("Expected ']'", _lexer.Position);

        return index;
    }

    private static TerminalValue ToTerminal(Token token) =>
        new(token.Value ?? string.Empty);
}
