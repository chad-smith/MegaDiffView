using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace MegaDiffView
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window, INotifyPropertyChanged
  {
    private Dictionary<string, string> _chunksDictionary;
    private readonly Color _greenBackground = Color.FromRgb(235, 255, 235);
    private readonly Color _redBackground = Color.FromRgb(255, 235, 235);
    private readonly Color _whiteBackground = Color.FromRgb(255, 255, 255);

    private List<string> _fileNames;
    private string _selectedItem;

    public List<string> FileNames
    {
      get => _fileNames;
      set { _fileNames = value; OnPropertyChanged(); }
    }

    public string SelectedItem
    {
      get => _selectedItem;
      set
      {
        _selectedItem = value;
        DiffContentBox.Inlines.Clear();

        if (value != null)
        {
          DiffContentBox.Inlines.AddRange(
            FormatDiffContent(value, _chunksDictionary[value])
          );
        }
      }
    }

    public MainWindow()
    {
      InitializeComponent();
      DataContext = this;
    }

    private IEnumerable<Inline> FormatDiffContent(string header, string content)
    {
      var lines = content.TrimEnd('\r','\n').Split("\r\n");
      var maxLineLength = Math.Max( 80, lines.Max(line => line.Length));

      var headerRowCount = GetHeaderRowCount(lines);
      var headerLines = FormatHeader( lines.Take(headerRowCount) );
      var blocks = GetBlocks(lines.Skip(headerRowCount));
      return headerLines.Concat(blocks.SelectMany(block => FormatBlock(block, maxLineLength)));
    }

    private int GetHeaderRowCount(string[] lines)
    {
      for (var i = 0; i < lines.Length; i++)
      {
        if (lines[i].StartsWith("@@"))
        {
          return i;
        }
      }

      return lines.Length;
    }

    private IEnumerable<DiffBlock> GetBlocks(IEnumerable<string> lines)
    {
      if (!lines.Any())
      {
        yield break;
      }

      DiffBlock block = null;

      foreach (var line in lines)
      {
        if (line.StartsWith("@@"))
        {
          if (block != null)
          {
            yield return block;
          }

          var info = BlockInfo.Parse( new Regex("@@ ([^@]+) @@").Match(line).Groups[1].Value);
          var header = line.Substring(line.LastIndexOf("@@")+2);
          if (string.IsNullOrWhiteSpace( header ))
          {
            header = Environment.NewLine;
          }
          block = new DiffBlock( header, info );
        }
        else
        {
          block.Lines.Add(line);
        }
      }

      yield return block;
    }

    private IEnumerable<Inline> FormatHeader(IEnumerable<string> headerLines)
    {
      return new Inline[]
      {
        new Run(string.Join("\r\n", headerLines)) {Background = new SolidColorBrush(Color.FromRgb(220,220,220))},
        new LineBreak(),
        new LineBreak()
      };
    }

    private IEnumerable<Inline> FormatBlock(DiffBlock block, int maxLineLength)
    {
      yield return new Bold(new Run(block.Header)
      {
        Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150))
      });
      yield return new LineBreak();

      var leftLineNumberOffset = 0;
      var rightLineNumberOffset = 0;
      var baselineNumber = block.Info.LeftStartLineNumber;

      foreach (var line in block.Lines)
      {
        int lineNumber;
        Color backgroundColor;

        if (line.StartsWith('-'))
        {
          lineNumber = baselineNumber + leftLineNumberOffset++;
          backgroundColor = _redBackground;
        } else if (line.StartsWith('+'))
        {
          lineNumber = baselineNumber++ + rightLineNumberOffset++;
          backgroundColor = _greenBackground;
        }
        else
        {
          lineNumber = baselineNumber++;
          backgroundColor = _whiteBackground;
        }

        yield return new Run( lineNumber.ToString().PadLeft(5,' ') + " " )
        {
          Foreground = new SolidColorBrush(Color.FromRgb(80, 140, 200)),
          Background = new SolidColorBrush(backgroundColor)
        };
        yield return FormatDiffLine( line, maxLineLength );
        yield return new LineBreak();
      }

      yield return new LineBreak();
    }

    private Inline FormatDiffLine(string line, int maxLineLength)
    {
      if (line.Length < 1)
      {
        return new Run(line);
      }

      var controlChar = line.Substring(0, 1);
      var color = Color.FromRgb(0, 0, 0);
      var background = Color.FromRgb(255,255,255);

      switch (controlChar)
      {
        case "-":
          color = Color.FromRgb(170, 25, 25);
          background = _redBackground;
          break;
        case "+":
          color = Color.FromRgb(25, 170, 25);
          background = _greenBackground;
          break;
      }

      return new Run(line.Substring(1).PadRight(maxLineLength))
      {
        Foreground = new SolidColorBrush(color),
        Background = new SolidColorBrush(background)
      };
    }
    
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog();
      var result = dialog.ShowDialog();

      if (result.GetValueOrDefault(false))
      {
        var diffFile = File.ReadAllText(dialog.FileName);
        var chunks = new Regex("diff --git ").Split(diffFile).Where(c => !string.IsNullOrEmpty(c));
        _chunksDictionary = chunks.ToDictionary(c => new Regex("([^ ]+)").Match(c).Groups[1].Value);

        FileNames = _chunksDictionary.Keys.ToList();
        SelectedItem = FileNames[0];
      }
    }
  }
}
