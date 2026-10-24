using Complot.Domain.Access;

namespace Complot.Domain.Tests.Access;

public sealed class AccessPolicyTests
{
    [Theory]
    //          admin  role            archived  view   edit
    [InlineData(true, null, false, true, true)]
    [InlineData(true, null, true, true, false)]
    [InlineData(false, SiteRole.Edit, false, true, true)]
    [InlineData(false, SiteRole.Edit, true, false, false)]
    [InlineData(false, SiteRole.View, false, true, false)]
    [InlineData(false, SiteRole.View, true, false, false)]
    [InlineData(false, null, false, false, false)]
    public void Site_rights(bool isAdmin, SiteRole? role, bool archived, bool canView, bool canEdit)
    {
        Assert.Equal(canView, AccessPolicy.CanView(isAdmin, role, archived));
        Assert.Equal(canEdit, AccessPolicy.CanEdit(isAdmin, role, archived));
    }

    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, false)]
    [InlineData(true, 0, false)]
    public void Pending_until_admin_or_member(bool isAdmin, int siteCount, bool pending) =>
        Assert.Equal(pending, AccessPolicy.IsPending(isAdmin, siteCount));
}
