namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 装备稀有度 - 五档，对标暗黑/DNF
    /// </summary>
    /// <remarks>
    /// 稀有度影响：
    /// - 词条数量（普通 1 条，传说 4 条）
    /// - 词条数值范围（高稀有度 roll 的数值区间更高）
    /// - 掉落概率（LootTable 按权重配）
    /// - 显示颜色（UI 层读这个枚举决定文字/边框颜色）
    /// 
    /// 枚举数值从 0 开始递增，方便当数组索引用。
    /// </remarks>
    public enum Rarity
    {
        Common = 0,     // 白 - 普通
        Uncommon = 1,   // 绿 - 精良
        Rare = 2,       // 蓝 - 稀有
        Epic = 3,       // 紫 - 史诗
        Legendary = 4,  // 橙 - 传说
    }

    /// <summary>
    /// 装备槽位 - 角色身上能穿戴装备的位置
    /// </summary>
    /// <remarks>
    /// DNF 风格：武器 + 防具 + 饰品，简化到 6 槽。
    /// 阶段 2 的 MVP 可以只做武器先验证掉落手感，其他槽后面逐步开放。
    /// </remarks>
    public enum EquipmentSlot
    {
        Weapon,     // 武器（金箍棒、九齿钉耙……）
        Armor,      // 防具（胸甲/袈裟）
        Accessory,  // 饰品（戒指/项链）
        Boots,      // 鞋子
        Helmet,     // 头盔/头饰
        Relic,      // 法宝/特殊（紧箍咒、芭蕉扇……）
    }
}
