namespace AutoStoreItems;

public class MyData
{
    //玩家数据表
    public List<ItemData> Items { get; set; } = new List<ItemData>();

    #region 数据结构
    public class ItemData
    {
        //玩家名字
        public string Name { get; set; }

        public bool IsActive { get; set; }

        //玩家出入服务器的记录时间
        public DateTime LogTime { get; set; }

        //自动识别模式
        public bool AutoMode { get; set; } = false;

        //装备识别模式
        public bool ArmorMode { get; set; } = false;

        //手持识别模式
        public bool HandMode { get; set; } = false;

        //监听储物空间位格开关
        public bool Bank { get; set; } = false;

        //自动储存提示
        public bool Mess { get; set; } = true;

        //清理速度
        public long UpdateRate { get; set; } = 10;

        public int Stack { get; set; }

        //自动储存表
        public List<string> ItemName { get; set; }

        public ItemData(string name = "",bool Active = true, bool enabled = true, bool auto = true, bool mess = true, DateTime time = default, long up = 10, List<string> item = null!)
        {
            this.Name = name ?? "";
            this.IsActive = Active;
            this.AutoMode = enabled;
            this.LogTime = time;
            this.Bank = auto;
            this.Mess = mess;
            this.UpdateRate = up;
            this.ItemName = item ?? new List<string>();
        }
    }

    #endregion
}
