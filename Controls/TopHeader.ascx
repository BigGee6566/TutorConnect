<%@ Control Language="VB" AutoEventWireup="false" CodeBehind="TopHeader.ascx.vb"
    Inherits="TutorConnect.Controls.TopHeader" %>

<div class="app-topbar">
    <%-- The hamburger only exists below 1024px; responsive.css hides it above that,
         while the bar itself now stays visible so the bell is always reachable. --%>
    <button type="button" class="hamburger" data-sidebar-open
            aria-controls="appSidebar" aria-expanded="false" aria-label="Open navigation">
        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor"
             stroke-width="2" stroke-linecap="round" aria-hidden="true" focusable="false">
            <path d="M3 6h18M3 12h18M3 18h18" />
        </svg>
    </button>

    <span class="app-topbar__title"><asp:Literal ID="litTitle" runat="server" Mode="Encode" /></span>

    <%-- BR-27. The count is spelled out in the accessible name, because a floating
         number beside an icon means nothing to a screen reader. --%>
    <asp:HyperLink ID="lnkBell" runat="server" CssClass="bell" NavigateUrl="~/Notifications/Index.aspx">
        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor"
             stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
             aria-hidden="true" focusable="false">
            <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
            <path d="M13.7 21a2 2 0 0 1-3.4 0" />
        </svg>
        <asp:Literal ID="litCount" runat="server" />
    </asp:HyperLink>
</div>
