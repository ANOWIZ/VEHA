using Veha.Api.Services;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Тесты NotificationService: создание, список, счётчик непрочитанных, отметка.</summary>
public class NotificationServiceTests
{
    [Fact]
    public async Task Notify_list_unread_and_mark_read()
    {
        using var db = new SqliteTestDb();
        var uid = Guid.NewGuid();

        await using (var ctx = db.NewContext())
        {
            var svc = new NotificationService(ctx);
            await svc.NotifyAsync(uid, "timesheet_rejected", "T1", "b1", "/x", default);
            await svc.NotifyAsync(uid, "action_assigned", "T2", null, null, default);
        }

        await using (var ctx = db.NewContext())
        {
            var svc = new NotificationService(ctx);
            Assert.Equal(2, await svc.UnreadCountAsync(uid, default));
            var all = await svc.ListForAsync(uid, false, 50, default);
            Assert.Equal(2, all.Count);

            var first = all[0];
            await svc.MarkReadAsync(uid, first.Id, default);
            Assert.Equal(1, await svc.UnreadCountAsync(uid, default));

            var unread = await svc.ListForAsync(uid, true, 50, default);
            Assert.Single(unread);
        }
    }

    [Fact]
    public async Task Mark_all_read_clears_unread()
    {
        using var db = new SqliteTestDb();
        var uid = Guid.NewGuid();
        await using (var ctx = db.NewContext())
        {
            var svc = new NotificationService(ctx);
            await svc.NotifyAsync(uid, "k", "a", null, null, default);
            await svc.NotifyAsync(uid, "k", "b", null, null, default);
        }

        await using (var ctx = db.NewContext())
        {
            var svc = new NotificationService(ctx);
            var n = await svc.MarkAllReadAsync(uid, default);
            Assert.Equal(2, n);
            Assert.Equal(0, await svc.UnreadCountAsync(uid, default));
        }
    }
}
