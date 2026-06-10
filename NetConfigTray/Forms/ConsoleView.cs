using System.Text;
using NetConfigTray.Helpers;

namespace NetConfigTray.Forms;

/// <summary>
/// A lightweight console view: a read-only monospace text surface that renders device output with
/// basic ANSI SGR colors, backspace, and carriage-return overwrite. Keystrokes are captured and
/// raised via <see cref="UserInput"/> (no local echo — devices echo their own input).
/// </summary>
public sealed class ConsoleView : RichTextBox
{
    private enum ParseState { Normal, Escape, Csi }

    private ParseState _state = ParseState.Normal;
    private readonly StringBuilder _csi = new();
    private Color _currentColor;

    public event Action<string>? UserInput;

    public ConsoleView()
    {
        _currentColor = AppTheme.TextPrimary;
        ReadOnly = true;
        Multiline = true;
        WordWrap = false;
        BorderStyle = BorderStyle.None;
        ScrollBars = RichTextBoxScrollBars.Both;
        BackColor = AppTheme.AppBackground;
        ForeColor = AppTheme.TextPrimary;
        Font = AppTheme.ValueFont;
        DetectUrls = false;
        Cursor = Cursors.IBeam;
        HideSelection = false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var sequence = keyData switch
        {
            Keys.Up => "\u001b[A",
            Keys.Down => "\u001b[B",
            Keys.Right => "\u001b[C",
            Keys.Left => "\u001b[D",
            Keys.Home => "\u001b[H",
            Keys.End => "\u001b[F",
            Keys.Delete => "\u001b[3~",
            _ => null
        };

        if (sequence is not null)
        {
            UserInput?.Invoke(sequence);
            return true;
        }

        // Ctrl+<letter> -> control byte (e.g. Ctrl+C = 0x03), but leave Ctrl+C/V copy/paste intact
        // only via the explicit handling below.
        if ((keyData & Keys.Control) == Keys.Control)
        {
            var key = keyData & Keys.KeyCode;
            if (key is >= Keys.A and <= Keys.Z)
            {
                var ctrlByte = (char)(key - Keys.A + 1);
                UserInput?.Invoke(ctrlByte.ToString());
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                UserInput?.Invoke("\r");
                e.Handled = e.SuppressKeyPress = true;
                return;
            case Keys.Back:
                UserInput?.Invoke("\b");
                e.Handled = e.SuppressKeyPress = true;
                return;
            case Keys.Tab:
                UserInput?.Invoke("\t");
                e.Handled = e.SuppressKeyPress = true;
                return;
            case Keys.Escape:
                UserInput?.Invoke("\u001b");
                e.Handled = e.SuppressKeyPress = true;
                return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        // Printable characters are forwarded to the device; control chars handled in OnKeyDown.
        if (!char.IsControl(e.KeyChar))
        {
            UserInput?.Invoke(e.KeyChar.ToString());
            e.Handled = true;
            return;
        }

        e.Handled = true;
    }

    /// <summary>Appends device output, interpreting basic ANSI/control sequences.</summary>
    public void AppendOutput(string text)
    {
        var pending = new StringBuilder();

        void Flush()
        {
            if (pending.Length > 0)
            {
                Write(pending.ToString(), _currentColor);
                pending.Clear();
            }
        }

        foreach (var ch in text)
        {
            switch (_state)
            {
                case ParseState.Normal:
                    HandleNormalChar(ch, pending, Flush);
                    break;
                case ParseState.Escape:
                    if (ch == '[')
                    {
                        _state = ParseState.Csi;
                        _csi.Clear();
                    }
                    else
                    {
                        _state = ParseState.Normal;
                    }

                    break;
                case ParseState.Csi:
                    if (ch is >= '@' and <= '~')
                    {
                        Flush();
                        if (ch == 'm')
                        {
                            ApplySgr(_csi.ToString());
                        }

                        _state = ParseState.Normal;
                    }
                    else
                    {
                        _csi.Append(ch);
                    }

                    break;
            }
        }

        Flush();
        ScrollToCaretEnd();
    }

    private void HandleNormalChar(char ch, StringBuilder pending, Action flush)
    {
        switch (ch)
        {
            case '\u001b':
                flush();
                _state = ParseState.Escape;
                break;
            case '\b':
                flush();
                Backspace();
                break;
            case '\r':
                flush();
                CarriageReturn();
                break;
            case '\a':
                break; // bell
            case '\n':
                pending.Append('\n');
                break;
            default:
                if (!char.IsControl(ch))
                {
                    pending.Append(ch);
                }

                break;
        }
    }

    private void Write(string value, Color color)
    {
        // Overwrite mode: if the caret is not at the end (after a CR), replace existing characters.
        foreach (var ch in value)
        {
            if (ch == '\n')
            {
                SelectionStart = TextLength;
                SelectionLength = 0;
                SelectionColor = color;
                AppendText("\n");
                continue;
            }

            if (SelectionStart < TextLength && Text[SelectionStart] != '\n')
            {
                SelectionLength = 1;
            }
            else
            {
                SelectionLength = 0;
            }

            SelectionColor = color;
            SelectedText = ch.ToString();
            SelectionStart += 1;
            SelectionLength = 0;
        }
    }

    private void Backspace()
    {
        if (SelectionStart > 0)
        {
            SelectionStart -= 1;
            SelectionLength = 1;
            if (SelectedText != "\n")
            {
                SelectedText = string.Empty;
            }
            else
            {
                SelectionStart += 1;
                SelectionLength = 0;
            }
        }
    }

    private void CarriageReturn()
    {
        var line = GetLineFromCharIndex(SelectionStart);
        var lineStart = GetFirstCharIndexFromLine(line);
        if (lineStart >= 0)
        {
            SelectionStart = lineStart;
            SelectionLength = 0;
        }
    }

    private void ApplySgr(string parameters)
    {
        var codes = parameters.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (codes.Length == 0)
        {
            _currentColor = AppTheme.TextPrimary;
            return;
        }

        foreach (var code in codes)
        {
            if (!int.TryParse(code, out var value))
            {
                continue;
            }

            _currentColor = value switch
            {
                0 => AppTheme.TextPrimary,
                30 or 90 => AppTheme.TextMuted,
                31 or 91 => AppTheme.Red,
                32 or 92 => AppTheme.Green,
                33 or 93 => AppTheme.Yellow,
                34 or 94 => AppTheme.Blue,
                35 or 95 => AppTheme.Magenta,
                36 or 96 => AppTheme.Cyan,
                37 or 97 => AppTheme.TextPrimary,
                _ => _currentColor
            };
        }
    }

    private void ScrollToCaretEnd()
    {
        SelectionStart = TextLength;
        SelectionLength = 0;
        ScrollToCaret();
    }
}
