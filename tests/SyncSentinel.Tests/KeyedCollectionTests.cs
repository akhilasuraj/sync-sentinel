using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The keyed-collection helper: the id-assignment / replace-by-id / remove-by-id
/// invariants shared by every config collection (jobs + exclusion sets), in one
/// pure place. ConfigService's named CRUD methods are thin pass-throughs over it.
/// </summary>
public class KeyedCollectionTests
{
    private sealed record Item(string Id, string Name) : IIdentified<Item>
    {
        public Item WithId(string id) => this with { Id = id };
    }

    [Fact]
    public void Add_assigns_an_id_when_missing_and_appends()
    {
        var (items, added) = KeyedCollection.Add<Item>([], new Item("", "a"), () => "id1");

        Assert.Equal("id1", added.Id);
        Assert.Equal(new[] { added }, items);
    }

    [Fact]
    public void Add_keeps_a_caller_supplied_id_and_skips_the_generator()
    {
        var generated = false;

        var (_, added) = KeyedCollection.Add<Item>([], new Item("keep", "a"), () => { generated = true; return "x"; });

        Assert.Equal("keep", added.Id);
        Assert.False(generated);
    }

    [Fact]
    public void Update_replaces_the_item_with_the_matching_id_leaving_others()
    {
        var a = new Item("1", "a");
        var b = new Item("2", "b");

        var updated = KeyedCollection.Update<Item>([a, b], new Item("2", "B"));

        Assert.Equal(new[] { a, new Item("2", "B") }, updated);
    }

    [Fact]
    public void Update_reports_absence_with_null()
    {
        var updated = KeyedCollection.Update<Item>([new Item("1", "a")], new Item("missing", "x"));

        Assert.Null(updated);
    }

    [Fact]
    public void Delete_removes_the_item_with_the_matching_id()
    {
        var a = new Item("1", "a");
        var b = new Item("2", "b");

        var kept = KeyedCollection.Delete<Item>([a, b], "1");

        Assert.Equal(new[] { b }, kept);
    }

    [Fact]
    public void Delete_reports_absence_with_null()
    {
        var kept = KeyedCollection.Delete<Item>([new Item("1", "a")], "missing");

        Assert.Null(kept);
    }
}
