using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using Microsoft.Win32;

namespace MegaDiffView
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Dictionary<string, string> _chunksDictionary;
        private DiffFile _selectedItem;
        private ObservableCollection<DiffFile> _diffProgress;
        private string _progressFile;

        public ObservableCollection<DiffFile> DiffProgress
        {
            get => _diffProgress;
            set { _diffProgress = value; OnPropertyChanged(); }
        }

        public DiffFile SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;

                if (value == null)
                {
                    return;
                }

                value.HasBeenViewed = true;
                if (value.HasChangedSinceLastViewed) {
                  value.ContentHash = GetShortHash(_chunksDictionary[value.Filename]);
                  value.HasChangedSinceLastViewed = false;
                }

                const string head = @"
  <style>
    html,body { 
      margin: 0;
    }
    body {
      min-width: 2000px;
      white-space: nowrap;
      font-family: Consolas;
      font-size: 10pt;
    }
    hr {
      color: #DDDDDD;
    }
    code {
      font-family: Consolas;
      margin: 0;
      white-space: pre;
    }
    .header-line {
      background: #EEEEEE;
    }
    .line-number {
      color: rgb(50, 180, 180);
      display: inline-block;
      width: 50px;
      text-align: right;
      margin-right: 10px;
    }
    .added {
      background-color: rgb(235,255,235);
    }
    .deleted {
      background-color: rgb(253,233,235);
    }
    .added code {
      color: rgb(25, 170, 25);
    }
    .deleted code {
      color: rgb(170, 25, 25);
    }
    .block-header {
      color: #555555;
    }
    .block-divider {
      width:50px;
      text-align:right;
      color: #555555;
    }
  </style>
";
                var body = string.Concat(FormatDiffContent(_chunksDictionary[value.Filename]));
                Browser.NavigateToString($@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""UTF-8"">
  {head}
</head>
<body>
  {body}
</body>
</html>");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveProgress();
        }

        private IEnumerable<string> FormatDiffContent(string content)
        {
            var lines = content.TrimEnd('\r', '\n').Split("\r\n");

            var headerRowCount = GetHeaderRowCount(lines);
            var headerLines = FormatHeader(lines.Take(headerRowCount));
            var blocks = GetBlocks(lines.Skip(headerRowCount));
            return headerLines.Concat(blocks.SelectMany((block, index) => FormatBlock(block, index == 0)));
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

                    var info = BlockInfo.Parse(new Regex("@@ ([^@]+) @@").Match(line).Groups[1].Value);
                    var header = line.Substring(line.LastIndexOf("@@") + 2);
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        header = string.Empty;
                    }
                    block = new DiffBlock(header, info);
                }
                else
                {
                    block.Lines.Add(line);
                }
            }

            yield return block;
        }

        private IEnumerable<string> FormatHeader(IEnumerable<string> headerLines)
        {
            return new[]
            {
        string.Join("\r\n", headerLines.Select( l => $"<div class=\"header-line\">{l}</div>")),
        "<br>"
      };
        }

        private IEnumerable<string> FormatBlock(DiffBlock block, bool isFirstBlock)
        {
            if (!isFirstBlock)
            {
                yield return "<br>";
            }

            if (string.IsNullOrWhiteSpace(block.Header))
            {
                if (!isFirstBlock)
                {
                    yield return $"<div class=\"block-divider\">{"...",-5}</div><br>";
                }
            }
            else
            {
                yield return $"<div class=\"block-header\">{block.Header}</div><br>";
            }

            var leftLineNumberOffset = 0;
            var rightLineNumberOffset = 0;
            var baselineNumber = block.Info.LeftStartLineNumber;

            foreach (var line in block.Lines)
            {
                int lineNumber;
                var cssClass = "";

                if (line.StartsWith('-'))
                {
                    lineNumber = baselineNumber + leftLineNumberOffset++;
                    cssClass = "deleted";
                }
                else if (line.StartsWith('+'))
                {
                    lineNumber = baselineNumber++ + rightLineNumberOffset++;
                    cssClass = "added";
                }
                else
                {
                    lineNumber = baselineNumber++;
                }

                yield return $"<div class=\"{cssClass}\"><span class=\"line-number\">{lineNumber,-5} </span>";
                yield return $"<code>";
                yield return HttpUtility.HtmlEncode(line.Substring(1));
                yield return "</code>";
                yield return "</div>";
            }

            yield return Environment.NewLine;
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
                var hashedFilepath = GetShortHash(dialog.FileName);
                _progressFile = Path.Combine(Path.GetTempPath(), hashedFilepath + ".diff-progress");

                DiffProgress = LoadProgress(_progressFile);

                var diffFile = File.ReadAllText(dialog.FileName);
                var chunks = new Regex("diff --git ").Split(diffFile).Where(c => !string.IsNullOrEmpty(c));
                _chunksDictionary = chunks.ToDictionary(c => new Regex("([^ ]+)").Match(c).Groups[1].Value);

                var filenames = _chunksDictionary.Keys.ToList();

                foreach (var filename in filenames)
                {
                    var existing = DiffProgress.SingleOrDefault(dp => dp.Filename == filename);
                    var contentHash = GetShortHash(_chunksDictionary[filename]);
                    if (existing == null)
                    {
                        DiffProgress.Add(new DiffFile(filename, false, contentHash));
                    }
                    else
                    {
                        if (existing.ContentHash != contentHash)
                        {
                            existing.HasChangedSinceLastViewed = true;
                            existing.HasBeenViewed = false;
                        }
                    }
                }

                var newFiles = filenames.Except(DiffProgress.Select(p => p.Filename));
                if (newFiles.Any())
                {
                    foreach (var file in newFiles)
                    {

                        SaveProgress();
                    }
                }

                SelectedItem = DiffProgress.First();
            }
        }

        private Lazy<MD5> _md5 = new Lazy<MD5>(() => MD5.Create());

        private string GetShortHash(string stringToHash)
        {
            var contentBytes = Encoding.ASCII.GetBytes(stringToHash);
            var base64 = Convert.ToBase64String(_md5.Value.ComputeHash(contentBytes));
            return base64.TrimEnd('=').Replace('/', '_');
        }

        private ObservableCollection<DiffFile> LoadProgress(string progressFilePath)
        {
            var diffProgress = new ObservableCollection<DiffFile>();
            if (!File.Exists(progressFilePath))
            {
                return diffProgress;
            }

            var progressLines = File.ReadAllLines(progressFilePath);
            foreach (var line in progressLines)
            {
                var lineValues = line.Split(":");
                diffProgress.Add(new DiffFile(lineValues[0], bool.Parse(lineValues[1]), lineValues[2]));
            }

            return diffProgress;
        }

        private void SaveProgress()
        {
            if (_progressFile == null)
            {
                return;
            }

            var contents = DiffProgress.Select(p => $"{p.Filename}:{p.HasBeenViewed}:{p.ContentHash}");
            File.WriteAllLines(_progressFile, contents);
        }

        public bool HasBeenViewed(string filename)
        {
            return DiffProgress.Any(f => f.Filename == filename && f.HasBeenViewed);
        }
    }

    public class DiffFile : INotifyPropertyChanged
    {
        private bool _hasBeenViewed;
        private bool _hasChangedSinceLastViewed;

        public DiffFile(string filename, bool hasBeenViewed, string contentHash)
        {
            Filename = filename;
            HasBeenViewed = hasBeenViewed;
            ContentHash = contentHash;
        }
        public string Filename { get; set; }
        public string ContentHash { get; set; }

        public bool HasBeenViewed
        {
            get => _hasBeenViewed;
            set
            {
                _hasBeenViewed = value;
                OnPropertyChanged();
            }
        }

        public bool HasChangedSinceLastViewed
        {
            get
            {
                return _hasChangedSinceLastViewed;
            }
            internal set
            {
                _hasChangedSinceLastViewed = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
