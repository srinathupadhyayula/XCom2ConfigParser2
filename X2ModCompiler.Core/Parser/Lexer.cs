using System.Buffers;

namespace X2ModCompiler.Core.Parser;

/// <summary>
/// Provides high-performance lexical analysis for XCOM 2 configuration strings using zero-allocation Spans.
/// This lexer decomposes raw text into a stream of tokens (delimiters, assignments, and values).
/// </summary>
public ref struct Lexer
{
    private static readonly SearchValues<char> Whitespace = SearchValues.Create(" \t");
    private static readonly SearchValues<char> Delimiters = SearchValues.Create("()[],=\"; \t");

    private readonly ReadOnlySpan<char> _text;
    private int _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lexer"/> struct with the specified target text.
    /// </summary>
    /// <param name="text">The raw configuration span to tokenize.</param>
    public Lexer(ReadOnlySpan<char> text)
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
    public Token Peek()
    {
        int savedPos = _position;
        Token token = Next();
        _position = savedPos;
        return token;
    }

    /// <summary>
    /// Returns the next token in the stream and advances the lexer position past it.
    /// </summary>
    /// <returns>The next available <see cref="Token"/>.</returns>
    public Token Next()
    {
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
        var remaining = _text.Slice(_position);
        int whitespaceCount = remaining.IndexOfAnyExcept(Whitespace);
        if (whitespaceCount == -1)
            _position = _text.Length;
        else
            _position += whitespaceCount;
    }

    /// <summary>
    /// Identifies and reads the next token based on the current character.
    /// </summary>
    private Token ReadToken()
    {
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
            
        return Token.Quoted(new Range(start, _position), start);
    }

    /// <summary>
    /// Reads a sequence of characters until a delimiter or whitespace is encountered.
    /// </summary>
    private Token ReadText()
    {
        int start = _position;
        var remaining = _text.Slice(_position);
        int delimiterIndex = remaining.IndexOfAny(Delimiters);
        
        if (delimiterIndex == -1)
            _position = _text.Length;
        else
            _position += delimiterIndex;
            
        return Token.TextValue(new Range(start, _position), start);
    }
}
