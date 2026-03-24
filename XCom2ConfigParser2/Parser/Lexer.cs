namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Provides lexical analysis for XCOM 2 configuration strings, specifically targeting complex struct and array values.
/// This lexer decomposes raw text into a stream of tokens (delimiters, assignments, and values).
/// </summary>
public sealed class Lexer
{
    private readonly string _text;
    private int _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lexer"/> class with the specified target text.
    /// </summary>
    /// <param name="text">The raw configuration string to tokenize.</param>
    public Lexer(string text)
    {
        _text = text;
        _position = 0;
    }

    /// <summary>
    /// Gets the current character position of the lexer within the source text.
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// Returns the next token in the stream without advancing the lexer position.
    /// </summary>
    /// <returns>The next available <see cref="Token"/>, or an EOF token if the end of text is reached.</returns>
    public Token? Peek()
    {
        if (_position >= _text.Length)
            return Token.Eof(_position);

        SkipWhitespace();

        if (_position >= _text.Length)
            return Token.Eof(_position);

        return ReadToken();
    }

    /// <summary>
    /// Returns the next token in the stream and advances the lexer position past it.
    /// </summary>
    /// <returns>The next available <see cref="Token"/>.</returns>
    public Token Next()
    {
        if (_position >= _text.Length)
            return Token.Eof(_position);

        SkipWhitespace();

        if (_position >= _text.Length)
            return Token.Eof(_position);

        return ReadToken();
    }

    /// <summary>
    /// Advances the lexer position past any leading space or tab characters.
    /// </summary>
    public void SkipWhitespace()
    {
        while (_position < _text.Length && (_text[_position] == ' ' || _text[_position] == '\t'))
            _position++;
    }

    /// <summary>
    /// Identifies and reads the next token based on the current character.
    /// </summary>
    private Token ReadToken()
    {
        if (_position >= _text.Length)
            return Token.Eof(_position);

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

    /// <summary>
    /// Reads a double-quoted string, handling escaped characters.
    /// </summary>
    private Token ReadQuotedString()
    {
        int start = _position++;
        while (_position < _text.Length && _text[_position] != '"')
        {
            // Handle escape sequences
            if (_text[_position] == '\\' && _position + 1 < _text.Length)
                _position += 2;
            else
                _position++;
        }
        if (_position < _text.Length)
            _position++; // closing quote
        return Token.Quoted(_text.Substring(start, _position - start), start);
    }

    /// <summary>
    /// Reads a sequence of characters until a delimiter or whitespace is encountered.
    /// </summary>
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
        return Token.TextValue(_text.Substring(start, _position - start), start);
    }
}
