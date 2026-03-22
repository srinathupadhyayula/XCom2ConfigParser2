namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Exception thrown when struct/array parsing fails.
/// </summary>
public sealed class ParseException : Exception
{
    public int Position { get; }

    public ParseException(string message, int position) : base(message)
    {
        Position = position;
    }
}

/// <summary>
/// Token types for the struct lexer.
/// </summary>
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
    Quoted,      // "string"
    Eof
}

/// <summary>
/// Token for struct parsing.
/// </summary>
public readonly struct Token
{
    public TokenType Type { get; }
    public string? Value { get; }
    public int Position { get; }

    public Token(TokenType type, string? value, int position)
    {
        Type = type;
        Value = value;
        Position = position;
    }

    public static Token LParen(int pos) => new(TokenType.LParen, null, pos);
    public static Token RParen(int pos) => new(TokenType.RParen, null, pos);
    public static Token LBrack(int pos) => new(TokenType.LBrack, null, pos);
    public static Token RBrack(int pos) => new(TokenType.RBrack, null, pos);
    public static Token Comma(int pos) => new(TokenType.Comma, null, pos);
    public static Token Eq(int pos) => new(TokenType.Eq, null, pos);
    public static Token Semi(int pos) => new(TokenType.Semi, null, pos);
    public static Token TextValue(string value, int pos) => new(TokenType.Text, value, pos);
    public static Token Quoted(string value, int pos) => new(TokenType.Quoted, value, pos);
    public static Token Eof(int pos) => new(TokenType.Eof, null, pos);
}
