<%@ Page Title="Audit Log" Language="VB" MasterPageFile="~/MasterPages/Admin.Master"
    AutoEventWireup="false" CodeBehind="AuditLog.aspx.vb"
    Inherits="TutorConnect.AdminArea.AuditLog" Async="true" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Audit Log - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <%-- Read-only, and there is no route anywhere in the application that edits or deletes
         an audit record. Web.config already denies everything under /Admin to anyone who is
         not an administrator. --%>
    <div class="filter-bar">
        <div class="form-row">
            <asp:Label ID="lblAction" runat="server" AssociatedControlID="ddlAction">Action</asp:Label>
            <asp:DropDownList ID="ddlAction" runat="server" AutoPostBack="true">
                <asp:ListItem Value="">All</asp:ListItem>
                <%-- Every option here is an action something actually writes. A filter that
                     can never match is the same defect as a button that does nothing. --%>
                <asp:ListItem Value="BookingCancelled">Booking cancelled</asp:ListItem>
                <asp:ListItem Value="BookingRescheduled">Booking rescheduled</asp:ListItem>
                <asp:ListItem Value="PostponementRequested">Postponement requested</asp:ListItem>
                <asp:ListItem Value="PostponementApproved">Postponement approved</asp:ListItem>
                <asp:ListItem Value="PostponementDeclined">Postponement declined</asp:ListItem>
                <asp:ListItem Value="PostponementWithdrawn">Postponement withdrawn</asp:ListItem>
                <asp:ListItem Value="TutorApproved">Tutor approved</asp:ListItem>
                <asp:ListItem Value="TutorRejected">Tutor approval withdrawn</asp:ListItem>
                <asp:ListItem Value="UserDeactivated">User deactivated</asp:ListItem>
                <asp:ListItem Value="UserActivated">User activated</asp:ListItem>
                <asp:ListItem Value="AdministratorCreated">Administrator created</asp:ListItem>
                <asp:ListItem Value="SubjectCreated">Subject created</asp:ListItem>
                <asp:ListItem Value="SubjectDeleted">Subject deleted</asp:ListItem>
                <asp:ListItem Value="SubjectActivated">Subject activated</asp:ListItem>
                <asp:ListItem Value="SubjectDeactivated">Subject deactivated</asp:ListItem>
                <asp:ListItem Value="ReviewHidden">Review hidden</asp:ListItem>
                <asp:ListItem Value="ReviewRestored">Review restored</asp:ListItem>
            </asp:DropDownList>
        </div>
        <div class="form-row">
            <asp:Label ID="lblEntity" runat="server" AssociatedControlID="ddlEntity">Record type</asp:Label>
            <asp:DropDownList ID="ddlEntity" runat="server" AutoPostBack="true">
                <asp:ListItem Value="">All</asp:ListItem>
                <asp:ListItem Value="Booking">Booking</asp:ListItem>
                <asp:ListItem Value="PostponementRequest">Postponement</asp:ListItem>
                <asp:ListItem Value="Person">Person</asp:ListItem>
                <asp:ListItem Value="Tutor">Tutor</asp:ListItem>
                <asp:ListItem Value="Subject">Subject</asp:ListItem>
                <asp:ListItem Value="Review">Review</asp:ListItem>
            </asp:DropDownList>
        </div>
    </div>

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">Audit log</h2>
            <span class="page-subtitle"><asp:Literal ID="litCount" runat="server" Mode="Encode" /></span>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptAudit" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Every recorded change, newest first</caption>
                        <thead><tr>
                            <th scope="col">When</th>
                            <th scope="col">Who</th>
                            <th scope="col">Role</th>
                            <th scope="col">Action</th>
                            <th scope="col">Record</th>
                            <th scope="col">Before</th>
                            <th scope="col">After</th>
                            <th scope="col">Reason</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="When"><%# GetTimestamp(Eval("CreatedAt")) %></td>
                        <td data-label="Who"><%#: GetActor(Container.DataItem) %></td>
                        <td data-label="Role"><%#: Eval("ActorRole") %></td>
                        <td data-label="Action"><%#: Eval("ActionType") %></td>
                        <td data-label="Record"><%#: GetRecord(Container.DataItem) %></td>
                        <td data-label="Before"><%#: GetOrDash(Eval("PreviousState")) %></td>
                        <td data-label="After"><%#: GetOrDash(Eval("NewState")) %></td>
                        <td data-label="Reason"><%#: GetOrDash(Eval("Reason")) %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">Nothing recorded yet</p>
                <p>Cancellations, postponements and administrator actions appear here.</p>
            </asp:Panel>
        </div>
    </div>

</asp:Content>
