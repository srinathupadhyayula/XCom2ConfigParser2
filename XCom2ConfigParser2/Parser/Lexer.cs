namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Lexer for struct/array tokenization.
/// </summary>
public sealed class Lexer
{
    private readonly string _text;
    private int _position;

    public Lexer(string text)
    {
        _text = text;
        _position = 0;
    }

    public int Position => _position;

    public Token? Peek()
    {
        if (_position >= _text.Length)
            return Token.Eof(_position);

        SkipWhitespace();

        if (_position >= _text.Length)
            return Token.Eof(_position);

        return ReadToken();
    }

    public Token Next()
    {
        if (_position >= _text.Length)
            return Token.Eof(_position);

        SkipWhitespace();

        if (_position >= _text.Length)
            return Token.Eof(_position);

        return ReadToken();
    }

    public void SkipWhitespace()
    {
        while (_position < _text.Length && (_text[_position] == ' ' || _text[_position] == '\t'))
            _position++;
    }

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
