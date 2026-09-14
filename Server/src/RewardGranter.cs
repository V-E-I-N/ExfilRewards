using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;

namespace ExfilRewards.Server;

/// <summary>
///     Builds the mail-attachment Items for a reward breakdown and sends them.
///     Mirrors the real GenerateUpdForItem + SplitStack + SendSystemMessageToPlayer
///     pattern used by the server's own "spt give" command, verified against
///     GiveSptCommand.cs in the SPT_Server_Dump source.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class RewardGranter(ItemHelper itemHelper, TemplateTable templateTable, MailSendService mailSendService)
{
    private const long MailMaxStorageSeconds = 172800L; // 48h, matches core mod convention seen in dump

    public void Grant(MongoId sessionId, RewardBreakdown breakdown)
    {
        var items = new List<Item>();

        AddCurrencyItems(items, Money.ROUBLES, breakdown.Total.Roubles);
        AddCurrencyItems(items, Money.DOLLARS, breakdown.Total.Dollars);
        AddCurrencyItems(items, Money.EUROS, breakdown.Total.Euros);

        if (items.Count == 0)
        {
            // Nothing to grant (e.g. all amounts rounded to zero) - do not send an empty mail.
            return;
        }

        mailSendService.SendSystemMessageToPlayer(
            sessionId,
            "Raid Bonus Rewards: extraction bonus + kill bonus for your last raid.",
            items,
            MailMaxStorageSeconds
        );
    }

    private void AddCurrencyItems(List<Item> items, MongoId currencyTpl, long amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var template = templateTable.Items[currencyTpl];

        var stack = new Item
        {
            Id = new MongoId(),
            Template = currencyTpl,
            Upd = itemHelper.GenerateUpdForItem(template)
        };
        stack.Upd!.StackObjectsCount = amount;

        // Currency stacks can exceed the item's max stack size at these
        // reward amounts, so split into multiple valid stacks the same
        // way the server's own "spt give" command does.
        items.AddRange(itemHelper.SplitStack(stack));
    }
}
