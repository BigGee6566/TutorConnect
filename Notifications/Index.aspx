<%@ Page Title="Notifications" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="Index.aspx.vb"
    Inherits="TutorConnect.NotificationArea.Index" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Notifications - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">Notifications</h2>
            <asp:Button ID="btnMarkAll" runat="server" Text="Mark all read" CssClass="btn btn--outline btn--sm" />
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptNotifications" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Your notifications, newest first</caption>
                        <thead><tr>
                            <th scope="col">When</th>
                            <th scope="col">Notification</th>
                            <th scope="col">Status</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="When"><%# GetWhen(Eval("CreatedAt")) %></td>
                        <td data-label="Notification">
                            <strong><%#: Eval("Title") %></strong><br />
                            <span class="page-subtitle"><%#: Eval("Message") %></span>
                        </td>
                        <%-- Unread is stated in words, not implied by weight alone (F.6). --%>
                        <td data-label="Status"><%# GetReadState(Eval("IsRead")) %></td>
                        <td data-label="Action" class="col-action">
                            <asp:LinkButton ID="btnRead" runat="server" CssClass="btn btn--outline btn--sm"
                                Visible='<%# Not CBool(Eval("IsRead")) %>'
                                CommandArgument='<%# Eval("NotificationId") %>'
                                OnCommand="MarkRead_Command">Mark read</asp:LinkButton>
                            <asp:HyperLink ID="lnkGo" runat="server" CssClass="btn btn--outline btn--sm"
                                Visible='<%# Not String.IsNullOrEmpty(CStr(Eval("LinkUrl"))) %>'
                                NavigateUrl='<%# Eval("LinkUrl") %>'>Open</asp:HyperLink>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">Nothing to read</p>
                <p>Notifications appear here when a request, session or review changes.</p>
            </asp:Panel>
        </div>
    </div>

</asp:Content>
