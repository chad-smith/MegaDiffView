namespace MegaDiffView
{
  internal class BlockInfo
  {
    public static BlockInfo Parse(string rawInfo)
    {
      var blockInfo = new BlockInfo();
      var rawBlockInfos = rawInfo.Split(' ');
      var (leftRaw, rightRaw) = (rawBlockInfos[0], rawBlockInfos[1]);
      (blockInfo.LeftStartLineNumber, blockInfo.LeftLineCount) = GetLineInfo(leftRaw);
      (blockInfo.RightStartLineNumber, blockInfo.RightLineCount) = GetLineInfo(rightRaw);

      return blockInfo;
    }

    public static (int, int) GetLineInfo(string raw)
    {
      var indexOfSplitter = raw.IndexOf(",");
      if (indexOfSplitter >= 0)
      {
        return (
          int.Parse(raw.Substring(1, indexOfSplitter - 1)),
          int.Parse(raw.Substring(indexOfSplitter + 1))
        );
      }

      return (int.Parse(raw), 0);
    }

    public int RightLineCount { get; set; }

    public int RightStartLineNumber { get; set; }

    public int LeftLineCount { get; set; }

    public int LeftStartLineNumber { get; set; }
  }
}