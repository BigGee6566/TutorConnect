<%@ Page Title="Manage Users" Language="VB" MasterPageFile="~/MasterPages/Admin.Master"
    AutoEventWireup="false" CodeBehind="ManageUsers.aspx.vb"
    Inherits="TutorConnect.AdminArea.ManageUsers" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Manage Users - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="filter-bar">
        <div class="form-row">
            <asp:Label ID="lblSearch" runat="server" AssociatedControlID="txtSearch">Search</asp:Label>
            <asp:TextBox ID="txtSearch" runat="server" MaxLength="100" placeholder="Name, username or email" />
        </div>
        <div class="form-row">
            <asp:Label ID="lblRole" runat="server" AssociatedControlID="ddlRole">Role</asp:Label>
            <asp:DropDownList ID="ddlRole" runat="server">
                <asp:ListItem Value="">All roles</asp:ListItem>
                <asp:ListItem Value="Student">Student</asp:ListItem>
                <asp:ListItem Value="Tutor">Tutor</asp:ListItem>
                <asp:ListItem Value="Administrator">Administrator</asp:ListItem>
            </asp:DropDownList>
        </div>
        <div class="form-row">
            <asp:Label ID="lblActive" runat="server" AssociatedControlID="ddlActive">Status</asp:Label>
            <asp:DropDownList ID="ddlActive" runat="server">
                <asp:ListItem Value="">All</asp:ListItem>
                <asp:ListItem Value="1">Active</asp:ListItem>
                <asp:ListItem Value="0">Deactivated</asp:ListItem>
            </asp:DropDownList>
        </div>
        <asp:Button ID="btnSearch" runat="server" Text="Search" CssClass="btn btn--primary" />
    </div>

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">Users</h2>
            <asp:HyperLink ID="lnkAddAdmin" runat="server" CssClass="btn btn--primary btn--sm"
                           NavigateUrl="~/Admin/Users/AddAdministrator.aspx">Add Administrator</asp:HyperLink>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptUsers" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">All user accounts</caption>
                        <thead><tr>
                            <th scope="col">Name</th>
                            <th scope="col">Username</th>
                            <th scope="col">Email</th>
                            <th scope="col">Role</th>
                            <th scope="col">Status</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Name"><%#: Eval("FullName") %></td>
                        <td data-label="Username"><%#: Eval("UserName") %></td>
                        <td data-label="Email"><%#: Eval("EmailAddress") %></td>
                        <td data-label="Role"><%#: Eval("Role") %></td>
                        <td data-label="Status"><%# GetStatus(Container.DataItem) %></td>
                        <td data-label="Action" class="col-action">
                            <%-- Only ever shown for a tutor still awaiting approval. --%>
                            <asp:LinkButton ID="btnApprove" runat="server" CssClass="btn btn--success btn--sm"
                                Visible='<%# NeedsApproval(Container.DataItem) %>'
                                CommandArgument='<%# Eval("PersonId") %>' OnCommand="Approve_Command">Approve Tutor</asp:LinkButton>

                            <asp:LinkButton ID="btnDeactivate" runat="server" CssClass="btn btn--danger btn--sm"
                                Visible='<%# CBool(Eval("IsActive")) %>'
                                CommandArgument='<%# Eval("PersonId") %>' OnCommand="Deactivate_Command"
                                OnClientClick="return confirm('Deactivate this account? They will be signed out on their next request.');">Deactivate</asp:LinkButton>

                            <asp:LinkButton ID="btnActivate" runat="server" CssClass="btn btn--outline btn--sm"
                                Visible='<%# Not CBool(Eval("IsActive")) %>'
                                CommandArgument='<%# Eval("PersonId") %>' OnCommand="Activate_Command">Activate</asp:LinkButton>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">No users match these filters</p>
                <p>Try a different search term, role or status.</p>
            </asp:Panel>
        </div>
    </div>

    <p class="form-row__hint">
        Passwords are stored only as a one-way hash. Nobody, including an administrator,
        can read or reset one from this screen.
    </p>

</asp:Content>
