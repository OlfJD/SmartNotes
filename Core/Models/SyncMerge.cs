using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartNotes.Core.Models;

/// <summary>A deleted note, remembered so another PC doesn't bring it back.</summary>
public class SyncTombstone
{
    public Guid Id { get; set; }
    public DateTime DeletedAt { get; set; }
}

/// <summary>
/// What one PC writes into the sync folder (notes-&lt;pc&gt;-&lt;id&gt;.json): all its notes and the notes it deleted.
/// Every PC writes only its own file, so two PCs never write the same file (no OneDrive conflict copies).
/// </summary>
public class SyncSnapshot
{
    public int Format { get; set; } = 1;
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public DateTime WrittenAt { get; set; } = DateTime.Now;
    public List<NoteItem> Notes { get; set; } = new();
    public List<SyncTombstone> Deleted { get; set; } = new();
}

/// <summary>What has to change on this PC after reading the other PCs' files.</summary>
public class SyncPlan
{
    /// <summary>Notes another PC has and this one doesn't (and didn't delete).</summary>
    public List<NoteItem> Added { get; } = new();
    /// <summary>Newer versions of notes this PC has, already carrying this PC's position and size.</summary>
    public List<NoteItem> Updated { get; } = new();
    /// <summary>Notes another PC deleted after their last change here.</summary>
    public List<Guid> Deleted { get; } = new();

    public bool IsEmpty => Added.Count == 0 && Updated.Count == 0 && Deleted.Count == 0;
}

/// <summary>
/// The sync rules, free of files and UI. Per note the newest change wins (content and look; see
/// <see cref="NoteItem.SyncStamp"/>), but where a note sits and how big it is stays per PC, because screens differ.
/// A deletion wins over every change made before it, and loses to a change made after it.
/// </summary>
public static class SyncMerge
{
    /// <summary>Deletions are remembered this long; a PC that was off longer may bring a deleted note back.</summary>
    public static readonly TimeSpan TombstoneLifetime = TimeSpan.FromDays(90);

    public static SyncPlan Plan(IReadOnlyList<NoteItem> local, IReadOnlyDictionary<Guid, DateTime> localDeleted, IEnumerable<SyncSnapshot> remotes)
    {
        var plan = new SyncPlan();
        var newestRemote = new Dictionary<Guid, NoteItem>();
        var remoteDeleted = new Dictionary<Guid, DateTime>();
        foreach (var snapshot in remotes)
        {
            foreach (var note in snapshot.Notes ?? new())
            {
                if (note == null || note.IsDeleted || note.Id == Guid.Empty) continue;
                if (!newestRemote.TryGetValue(note.Id, out var seen) || note.SyncStamp > seen.SyncStamp) newestRemote[note.Id] = note;
            }
            foreach (var t in snapshot.Deleted ?? new())
            {
                if (t == null) continue;
                if (!remoteDeleted.TryGetValue(t.Id, out var at) || t.DeletedAt > at) remoteDeleted[t.Id] = t.DeletedAt;
            }
        }

        var localById = local.ToDictionary(n => n.Id);
        foreach (var remote in newestRemote.Values)
        {
            // deleted here after that version, or deleted on some PC after it: it stays gone
            if (localDeleted.TryGetValue(remote.Id, out var deletedHere) && deletedHere >= remote.SyncStamp) continue;
            if (remoteDeleted.TryGetValue(remote.Id, out var deletedThere) && deletedThere >= remote.SyncStamp) continue;

            if (!localById.TryGetValue(remote.Id, out var mine))
            {
                plan.Added.Add(remote);
            }
            else if (remote.SyncStamp > mine.SyncStamp)
            {
                var updated = Copy(remote);
                updated.X = mine.X;
                updated.Y = mine.Y;
                updated.Width = mine.Width;
                updated.Height = mine.Height;
                plan.Updated.Add(updated);
            }
        }

        foreach (var (id, deletedAt) in remoteDeleted)
        {
            if (localById.TryGetValue(id, out var mine) && deletedAt > mine.SyncStamp && !plan.Updated.Any(u => u.Id == id))
            {
                plan.Deleted.Add(id);
            }
        }
        return plan;
    }

    /// <summary>An independent copy (the plan must not share lists with the snapshot it came from).</summary>
    public static NoteItem Copy(NoteItem n) =>
        System.Text.Json.JsonSerializer.Deserialize<NoteItem>(System.Text.Json.JsonSerializer.Serialize(n))!;
}
