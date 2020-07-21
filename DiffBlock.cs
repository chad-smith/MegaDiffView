using System.Collections.Generic;

namespace MegaDiffView
{
  internal class DiffBlock
  {
    public DiffBlock(string header, BlockInfo info)
    {
      Header = header;
      Info = info;
    }

    public string Header { get; }
    public BlockInfo Info { get; }
    public List<string> Lines { get; } = new List<string>();
  }
}