using System.Text;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace AutoStoreItems;

[ApiVersion(2, 1)]
public class AutoStoreItems : TerrariaPlugin
{

    #region 插件信息
    public override string Name => "自动存储";
    public override string Author => "羽学 cmgy雱";
    public override Version Version => new Version(1, 2, 7);
    public override string Description => "涡轮增压不蒸鸭";
    #endregion

    #region 注册与释放
    public AutoStoreItems(Main game) : base(game) { }
    public override void Initialize()
    {
        LoadConfig();
        GeneralHooks.ReloadEvent += ReloadConfig;
        ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        GetDataHandlers.PlayerUpdate.Register(this.PlayerUpdate);
        ServerApi.Hooks.GameUpdate.Register(this, this.OnGameUpdate);
        TShockAPI.Commands.ChatCommands.Add(new Command("AutoStore.use", Commands.Ast, "ast", "自存"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= ReloadConfig;
            ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            GetDataHandlers.PlayerUpdate.UnRegister(this.PlayerUpdate);
            ServerApi.Hooks.GameUpdate.Deregister(this, this.OnGameUpdate);
            TShockAPI.Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == Commands.Ast);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 配置重载读取与写入方法
    internal static Configuration Config = new();
    internal static MyData Data = new();
    private void ReloadConfig(ReloadEventArgs args)
    {
        LoadConfig();
        args.Player.SendInfoMessage(GetString("[自动存储]重新加载配置完毕。"));
    }
    private static void LoadConfig()
    {
        if (!File.Exists(Configuration.FilePath))
        {
            Config = Configuration.Read();
            Config.Write();
        }
        else
        {
            Config.Write();
        }
    }
    #endregion

    #region 玩家更新配置方法（计入记录时间并创建配置结构）
    private static int ClearCount = 0; //需要清理的玩家计数
    public readonly static List<TSPlayer> ActivePlayer = new();
    private void OnJoin(JoinEventArgs args)
    {
        if (args == null || !Config.open)
        {
            return;
        }

        var plr = TShock.Players[args.Who];

        if (plr == null)
        {
            return;
        }

        // 查找玩家数据
        var data = Data.Items.FirstOrDefault(x => x.Name == plr.Name);

        // 如果玩家不在数据表中，则创建新的数据条目
        if (data == null || plr.Name != data.Name)
        {
            Data.Items.Add(new MyData.ItemData()
            {
                Name = plr.Name,
                AutoMode = true,
                IsActive = true,
                Bank = true,
                LogTime = DateTime.Now,
                Mess = true,
                UpdateRate = 10,
                ItemName = new List<string>()
            });
        }
        else
        {
            // 更新玩家的登录时间和活跃状态
            data.LogTime = DateTime.Now;
            data.IsActive = true;
        }

        //清理数据方法 
        if (Config.ClearData && data != null)
        {
            // 获取当前在线玩家的名字列表
            var active = ActivePlayer.Where(p => p != null && p.Active).Select(p => p.Name).ToList();

            if (!active.Contains(data.Name))
            {
                data.IsActive = false;
            }

            //清理条件
            var Remove = Data.Items.Where(list => list != null && list.LogTime != default &&
            (DateTime.Now - list.LogTime).TotalHours >= Config.timer).ToList();

            //数据清理的播报内容
            var mess = new StringBuilder();
            mess.AppendLine($"[i:3455][c/AD89D5:自][c/D68ACA:动][c/DF909A:垃][c/E5A894:圾][c/E5BE94:桶][i:3454]");
            mess.AppendLine($"以下玩家离线时间 与 [c/ABD6C7:{plr.Name}] 加入时间\n【[c/A1D4C2:{DateTime.Now}]】\n" +
                $"超过 [c/E17D8C:{Config.timer}] 小时 已清理 [c/76D5B4:自动自动储存] 数据：");

            foreach (var plr2 in Remove)
            {
                //只显示小时数 F0整数 F1保留1位小数 F2保留2位 如：24.01小时
                var hours = (DateTime.Now - plr2.LogTime).TotalHours;
                FormattableString Hours = $"{hours:F0}";

                //更新时间超过Config预设的时间，并该玩家更新状态为false则添加计数并移除数据
                if (hours >= Config.timer && !plr2.IsActive)
                {
                    ClearCount++;
                    mess.AppendFormat("[c/A7DDF0:{0}]:[c/74F3C9:{1}小时], ", plr2.Name, Hours);
                    Data.Items.Remove(plr2);
                }
            }

            //确保有一个玩家计数，只播报一次
            if (ClearCount > 0 && mess.Length > 0)
            {
                //广告开关
                if (Config.Enabled)
                {
                    //自定义广告内容
                    mess.AppendLine(Config.Advertisement);
                }

                TShock.Utils.Broadcast(mess.ToString(), 247, 244, 150);
                ClearCount = 0;
            }
        }
    }
    #endregion

    #region 玩家离开服务器更新记录时间
    private void OnLeave(LeaveEventArgs args)
    {
        if (args == null || !Config.open)
        {
            return;
        }

        var plr = TShock.Players[args.Who];
        var list = Data.Items.FirstOrDefault(x => x != null && x.Name == plr.Name);
        if (plr == null || list == null)
        {
            return;
        }

        if (Config.ClearData)
        {
            //离开服务器更新记录时间与活跃状态
            if (!plr.Active && plr.Name == list.Name)
            {
                if (list.IsActive)
                {
                    list.LogTime = DateTime.Now;
                    list.IsActive = false;
                }
            }
        }
    }
    #endregion

    #region 检测背包持有物品方法
    public static long Timer = 0L;
    private void OnGameUpdate(EventArgs args)
    {
        Timer++;

        if (!Config.open)
        {
            return;
        }

        foreach (var plr in TShock.Players.Where(plr => plr != null && plr.Active && plr.IsLoggedIn && Config.open))
        {
            var list = Data.Items.FirstOrDefault(x => x.Name == plr.Name);

            if (list == null) continue;

            for (int i = 0; i < plr.TPlayer.inventory.Length; i++)
            {
                var inv = plr.TPlayer.inventory[i];

                //自动存物品
                if (list.AutoMode && !list.HandMode)
                {
                    if (Config.BankItems.Contains(inv.type) && Timer % list.UpdateRate == 0)
                    {
                        StoreItemInBanks(plr, inv, list.Bank, list.Mess, list.ItemName);

                        for (int i2 = 71; i2 <= 74; i2++)
                        {
                            CoinToBank(plr, i2);
                        }
                        break;
                    }
                }
                //手持储存
                else
                {
                    if (Config.BankItems.Contains(plr.TPlayer.inventory[plr.TPlayer.selectedItem].type))
                    {
                        StoreItemInBanks(plr, inv, list.Bank, list.Mess, list.ItemName);

                        for (int i2 = 71; i2 <= 74; i2++)
                        {
                            CoinToBank(plr, i2);
                        }

                        break;
                    }
                }
            }
        }
    }
    #endregion

    #region 判断物品存到哪个空间的方法
    private bool StoreItemInBanks(TSPlayer plr, Item inv, bool Auto, bool mess, List<string> List)
    {
        var Stored = false;
        Stored |= Config.bank1 && AutoStoredItem(plr, plr.TPlayer.bank.item, PlayerItemSlotID.Bank1_0, GetString("存钱罐"), Auto, mess, List);
        Stored |= Config.bank2 && AutoStoredItem(plr, plr.TPlayer.bank2.item, PlayerItemSlotID.Bank2_0, GetString("保险箱"), Auto, mess, List);
        Stored |= Config.bank3 && AutoStoredItem(plr, plr.TPlayer.bank3.item, PlayerItemSlotID.Bank3_0, GetString("护卫熔炉"), Auto, mess, List);
        Stored |= Config.bank4 && AutoStoredItem(plr, plr.TPlayer.bank4.item, PlayerItemSlotID.Bank4_0, GetString("虚空袋"), Auto, mess, List);
        return Stored;
    }
    #endregion

    #region 检测装备物品方法
    private void PlayerUpdate(object? sender, GetDataHandlers.PlayerUpdateEventArgs e)
    {
        var plr = e.Player;
        if (plr == null) return;

        var list = Data.Items.FirstOrDefault(x => x.Name == plr.Name);

        if (list == null || !list.ArmorMode) return;

        var Stored = false;
        foreach (var item in Config.ArmorItem)
        {
            var armor = plr.TPlayer.armor.Take(10).Where(x => x.netID == item).ToList();
            var hasArmor = armor.Any();

            if (hasArmor)
            {
                for (int i = 71; i <= 74; i++)
                {
                    CoinToBank(plr, i);
                }

                Stored |= Config.bank1 && AutoStoredItem(plr, plr.TPlayer.bank.item, PlayerItemSlotID.Bank1_0, GetString("存钱罐"), list.Bank, list.Mess, list.ItemName);
                Stored |= Config.bank2 && AutoStoredItem(plr, plr.TPlayer.bank2.item, PlayerItemSlotID.Bank2_0, GetString("保险箱"), list.Bank, list.Mess, list.ItemName);
                Stored |= Config.bank3 && AutoStoredItem(plr, plr.TPlayer.bank3.item, PlayerItemSlotID.Bank3_0, GetString("护卫熔炉"), list.Bank, list.Mess, list.ItemName);
                Stored |= Config.bank4 && AutoStoredItem(plr, plr.TPlayer.bank4.item, PlayerItemSlotID.Bank4_0, GetString("虚空袋"), list.Bank, list.Mess, list.ItemName);

                if (Stored) break;
            }
        }
    }
    #endregion

    #region 自动储存物品方法
    public static bool AutoStoredItem(TSPlayer tplr, Item[] bankItems, int bankSlot, string bankName, bool Auto, bool mess, List<string> List)
    {
        if (!tplr.IsLoggedIn || tplr == null) return false;

        Player plr = tplr.TPlayer;
        HashSet<string> itemName = new HashSet<string>(Data.Items.SelectMany(x => x.ItemName));

        foreach (var bank in bankItems)
        {
            for (int i = 0; i < plr.inventory.Length; i++)
            {
                var inv = plr.inventory[i];
                var data = Data.Items.FirstOrDefault(x => x.ItemName.Contains(inv.Name));

                if (Auto)
                {
                    if (!bank.IsAir && !bank.IsACoin && !List.Contains(bank.Name) )
                    {
                        List.Add(bank.Name);

                        if (mess)
                        {
                            tplr.SendMessage($"已将 '[c/92C5EC:{bank.Name}]'添加到自动储存表|指令菜单:[c/A1D4C2:/ast]", 255, 246, 158);
                        }
                    }
                }

                if (data != null
                    && inv.stack >= data.Stack
                    && itemName.Contains(inv.Name)
                    && inv.type == bank.type
                    && inv.type != plr.inventory[plr.selectedItem].type)
                {

                    bank.stack += inv.stack;
                    inv.TurnToAir();

                    if (bank.stack >= Item.CommonMaxStack) bank.stack = Item.CommonMaxStack;

                    tplr.SendData(PacketTypes.PlayerSlot, null, tplr.Index, PlayerItemSlotID.Inventory0 + i);
                    tplr.SendData(PacketTypes.PlayerSlot, null, tplr.Index, bankSlot + Array.IndexOf(bankItems, bank));

                    if (mess) 
                    { 
                        tplr.SendMessage(GetString($"【自动储存】已将'[c/92C5EC:{bank.Name}]'存入您的{bankName} 当前数量: {bank.stack}"), 255, 246, 158);
                    }
                    return true;
                }
            }
        }
        return false;
    }
    #endregion

    #region 自动存钱到存钱罐方法
    private static void CoinToBank(TSPlayer tplr, int coin)
    {
        Player plr = tplr.TPlayer;
        Item bankItem = new Item();
        int bankSolt = -1;

        for (int i2 = 0; i2 < 40; i2++)
        {
            bankItem = plr.bank.item[i2];
            if (bankItem.IsAir || bankItem.netID == coin)
            {
                bankSolt = i2;
                break;
            }
        }

        if (bankSolt != -1)
        {
            for (int i = 50; i < 54; i++)
            {
                Item invItem = plr.inventory[i];
                if (invItem.netID == coin)
                {
                    invItem.netID = 0;
                    tplr.SendData(PacketTypes.PlayerSlot, "", tplr.Index, i);

                    bankItem.netID = coin;
                    bankItem.type = invItem.type;
                    bankItem.stack += invItem.stack;

                    if (bankItem.stack >= 100 && coin != 74)
                    {
                        bankItem.stack %= 100;
                        tplr.GiveItem(coin + 1, 1);
                    }

                    else if (bankItem.stack >= Item.CommonMaxStack && coin == 74)
                    {
                        bankItem.stack = Item.CommonMaxStack;
                    }

                    tplr.SendData(PacketTypes.PlayerSlot, "", tplr.Index, PlayerItemSlotID.Bank1_0 + bankSolt);
                    break;
                }
            }
        }
    }
    #endregion

}
