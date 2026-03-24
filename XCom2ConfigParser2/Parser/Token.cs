namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Represents an error that occurs during the parsing of complex Unreal Engine 3 configuration values.
/// Includes the specific character position where the error was detected.
/// </summary>
public sealed class ParseException : Exception
{
    /// <summary>
    /// Gets the character position within the source text where the parsing error occurred.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class.
    /// </summary>
    /// <param name="message">The descriptive error message.</param>
    /// <param name="position">The character position of the failure.</param>
    public ParseException(string message, int position) : base(message)
    {
        Position = position;
    }
}

/// <summary>
/// Categorizes the various types of atomic units (tokens) recognized by the <see cref="Lexer"/>.
/// </summary>
public enum TokenType
{
    /// <summary>Opening parenthesis '('. Used for starting structs or arrays.</summary>
    LParen,

    /// <summary>Closing parenthesis ')'. Used for ending structs or arrays.</summary>
    RParen,

    /// <summary>Opening square bracket '['. Used for array index suffixes in property names.</summary>
    LBrack,

    /// <summary>Closing square bracket ']'. Used for array index suffixes.</summary>
    RBrack,

    /// <summary>Comma ','. Used as a delimiter between struct fields or array elements.</summary>
    Comma,

    /// <summary>Equals sign '='. Used for assignment within structs.</summary>
    Eq,

    /// <summary>Semicolon ';'. Used for comments.</summary>
    Semi,

    /// <summary>Unquoted text representing an identifier or a raw value.</summary>
    Text,

    /// <summary>Double-quoted string value.</summary>
    Quoted,

    /// <summary>End of the input text stream.</summary>
    Eof
}

/// <summary>
/// Represents a single atomic unit of configuration syntax, such as a delimiter, an identifier, or a value.
/// </summary>
public readonly struct Token
{
    /// <summary>
    /// Gets the category of this token.
    /// </summary>
    public TokenType Type { get; }

    /// <summary>
    /// Gets the raw string value associated with this token, or null if it represents a symbol.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Gets the character position in the source text where this token begins.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct.
    /// </summary>
    /// <param name="type">The type of the token.</param>
    /// <param name="value">The optional value string.</param>
    /// <param name="position">The source position.</param>
    public Token(TokenType type, string? value, int position)
    {
        Type = type;
        Value = value;
        Position = position;
    }

    /// <summary>Creates an opening parenthesis token.</summary>
    public static Token LParen(int pos) => new(TokenType.LParen, null, pos);
    /// <summary>Creates a closing parenthesis token.</summary>
    public static Token RParen(int pos) => new(TokenType.RParen, null, pos);
    /// <summary>Creates an opening square bracket token.</summary>
    public static Token LBrack(int pos) => new(TokenType.LBrack, null, pos);
    /// <summary>Creates a closing square bracket token.</summary>
    public static Token RBrack(int pos) => new(TokenType.RBrack, null, pos);
    /// <summary>Creates a comma delimiter token.</summary>
    public static Token Comma(int pos) => new(TokenType.Comma, null, pos);
    /// <summary>Creates an equals sign assignment token.</summary>
    public static Token Eq(int pos) => new(TokenType.Eq, null, pos);
    /// <summary>Creates a semicolon comment token.</summary>
    public static Token Semi(int pos) => new(TokenType.Semi, null, pos);
    /// <summary>Creates an unquoted text value token.</summary>
    public static Token TextValue(string value, int pos) => new(TokenType.Text, value, pos);
    /// <summary>Creates a double-quoted string token.</summary>
    public static Token Quoted(string value, int pos) => new(TokenType.Quoted, value, pos);
    /// <summary>Creates an end-of-file token.</summary>
    public static Token Eof(int pos) => new(TokenType.Eof, null, pos);
}
