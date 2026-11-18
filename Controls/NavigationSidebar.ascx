<%@ Control Language="VB" AutoEventWireup="false" CodeBehind="NavigationSidebar.ascx.vb"
    Inherits="TutorConnect.Controls.NavigationSidebar" %>

<div class="sidebar-backdrop" data-sidebar-close></div>

<nav class="sidebar" id="appSidebar" aria-label="Main navigation">

    <%-- Mobile drawer close control. E1 shows this as the x at the top of the sidebar;
         it is not a window close button. --%>
    <button type="button" class="sidebar__close" data-sidebar-close aria-label="Close navigation">
        <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor"
             stroke-width="2" aria-hidden="true" focusable="false">
            <path d="M18 6 6 18M6 6l12 12" />
        </svg>
    </button>

    <div class="sidebar__user">
        <span class="sidebar__avatar" aria-hidden="true"><asp:Literal ID="litInitials" runat="server" Mode="Encode" /></span>
        <span class="sidebar__name"><asp:Literal ID="litFullName" runat="server" Mode="Encode" /></span>
        <span class="sidebar__role"><asp:Literal ID="litRole" runat="server" Mode="Encode" /></span>
    </div>

    <div class="sidebar__nav">
        <asp:Repeater ID="rptNav" runat="server">
            <ItemTemplate>
                <a class="sidebar__link"
                   href="<%# GetUrl(Container.DataItem) %>"
                   <%# GetCurrentAttribute(Container.DataItem) %>>
                    <%# GetIcon(Container.DataItem) %>
                    <span><%# GetText(Container.DataItem) %></span>
                    <%# GetCountPill(Container.DataItem) %>
                </a>
            </ItemTemplate>
        </asp:Repeater>

        <div class="sidebar__logout">
            <a class="sidebar__link" href="<%: ResolveUrl("~/Account/Logout.aspx") %>">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                     aria-hidden="true" focusable="false">
                    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                    <path d="m16 17 5-5-5-5M21 12H9" />
                </svg>
                <span>Logout</span>
            </a>
        </div>
    </div>

</nav>
