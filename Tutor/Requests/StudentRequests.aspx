<%@ Page Title="Student Requests" Language="VB" MasterPageFile="~/MasterPages/Tutor.Master"
    AutoEventWireup="false" CodeBehind="StudentRequests.aspx.vb"
    Inherits="TutorConnect.TutorArea.StudentRequests" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>
<%@ Register TagPrefix="tc" TagName="StatusBadge" Src="~/Controls/StatusBadge.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Student Requests - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <%-- Second stage of a decline. Rendered on the server rather than as a per-row
         textbox: the table already carries eight columns, and a panel that appears only
         when needed keeps the rows intact at 375px. --%>
    <asp:Panel ID="pnlDecline" runat="server" CssClass="panel" Visible="false"
               DefaultButton="btnConfirmDecline" style="margin-bottom:24px">
        <div class="panel__header">
            <h2 class="panel__title">Decline request <asp:Literal ID="litDeclineId" runat="server" Mode="Encode" /></h2>
        </div>
        <div class="form-card">
            <div class="form-row">
                <asp:Label ID="lblReason" runat="server" AssociatedControlID="txtDeclineReason">Reason (optional)</asp:Label>
                <asp:TextBox ID="txtDeclineReason" runat="server" TextMode="MultiLine" MaxLength="200"
                             placeholder="The student will see this. For example: I am no longer free at that time." />
                <p class="form-row__hint">200 characters maximum. Leave blank to decline without a reason.</p>
            </div>
            <div class="form-actions">
                <asp:Button ID="btnConfirmDecline" runat="server" Text="Confirm Decline" CssClass="btn btn--danger" />
                <asp:Button ID="btnCancelDecline" runat="server" Text="Cancel" CssClass="btn btn--ghost"
                            CausesValidation="false" />
            </div>
        </div>
    </asp:Panel>

    <div class="panel">
        <div class="panel__header"><h2 class="panel__title">Student Requests</h2></div>

        <div class="table-responsive">
            <asp:Repeater ID="rptRequests" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Session requests sent to you</caption>
                        <thead><tr>
                            <th scope="col" class="numeric">Request ID</th>
                            <th scope="col">Student</th>
                            <th scope="col">Subject</th>
                            <th scope="col">Level</th>
                            <th scope="col">Date</th>
                            <th scope="col">Time</th>
                            <th scope="col">Status</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Request ID" class="numeric"><%#: Eval("RequestId") %></td>
                        <td data-label="Student"><%#: Eval("StudentName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Level"><%#: Eval("StudyLevelName") %></td>
                        <td data-label="Date"><%# GetDate(Eval("RequestedDate")) %></td>
                        <td data-label="Time"><%#: Eval("TimeRange") %></td>
                        <td data-label="Status">
                            <tc:StatusBadge ID="badge" runat="server" Status='<%# Eval("Status").ToString() %>' />
                        </td>
                        <td data-label="Action" class="col-action">
                            <%-- Accept and Decline appear only while the request is Pending;
                                 every other state shows an em dash, exactly as in the design. --%>
                            <asp:LinkButton ID="btnAccept" runat="server" CssClass="btn btn--success btn--sm"
                                Visible='<%# Eval("Status").ToString() = "Pending" %>'
                                CommandArgument='<%# Eval("RequestId") %>' OnCommand="Accept_Command">Accept</asp:LinkButton>
                            <%-- Opens the reason panel; the decline itself happens there. --%>
                            <asp:LinkButton ID="btnDecline" runat="server" CssClass="btn btn--danger btn--sm"
                                Visible='<%# Eval("Status").ToString() = "Pending" %>'
                                CommandArgument='<%# Eval("RequestId") %>' OnCommand="Decline_Command">Decline</asp:LinkButton>
                            <asp:Label ID="lblNoAction" runat="server" CssClass="no-action" Text="&#8212;"
                                Visible='<%# Eval("Status").ToString() <> "Pending" %>' />
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">No requests yet</p>
                <p>Requests appear here when a student asks for one of your published times.</p>
            </asp:Panel>
        </div>

        <div class="panel__footer">
            <asp:Button ID="btnRefresh" runat="server" Text="Refresh" CssClass="btn btn--outline btn--sm" />
        </div>
    </div>

</asp:Content>
