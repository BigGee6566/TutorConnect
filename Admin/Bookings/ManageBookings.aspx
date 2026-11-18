<%@ Page Title="Bookings" Language="VB" MasterPageFile="~/MasterPages/Admin.Master"
    AutoEventWireup="false" CodeBehind="ManageBookings.aspx.vb"
    Inherits="TutorConnect.AdminArea.ManageBookings" Async="true" %>

<%@ Register TagPrefix="tc" TagName="StatusBadge" Src="~/Controls/StatusBadge.ascx" %>
<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Bookings - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <%-- Mostly oversight. The one thing an administrator can change is to cancel a
         scheduled session on someone's behalf - which requires a reason, tells both
         parties, and is written to the audit log (Part 12.7). --%>
    <div class="filter-bar">
        <div class="form-row">
            <asp:Label ID="lblStatus" runat="server" AssociatedControlID="ddlRequestStatus">Request status</asp:Label>
            <asp:DropDownList ID="ddlRequestStatus" runat="server" AutoPostBack="true">
                <asp:ListItem Value="">All</asp:ListItem>
                <asp:ListItem Value="Pending">Pending</asp:ListItem>
                <asp:ListItem Value="Confirmed">Confirmed</asp:ListItem>
                <asp:ListItem Value="Declined">Declined</asp:ListItem>
                <asp:ListItem Value="Cancelled">Cancelled</asp:ListItem>
            </asp:DropDownList>
        </div>
    </div>

    <div class="panel" style="margin-bottom:24px">
        <div class="panel__header"><h2 class="panel__title">All requests</h2></div>
        <div class="table-responsive">
            <asp:Repeater ID="rptRequests" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Every session request on the platform</caption>
                        <thead><tr>
                            <th scope="col" class="numeric">ID</th>
                            <th scope="col">Student</th>
                            <th scope="col">Tutor</th>
                            <th scope="col">Subject</th>
                            <th scope="col">Date</th>
                            <th scope="col">Time</th>
                            <th scope="col">Status</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="ID" class="numeric"><%#: Eval("RequestId") %></td>
                        <td data-label="Student"><%#: Eval("StudentName") %></td>
                        <td data-label="Tutor"><%#: Eval("TutorName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Date"><%# GetDate(Eval("RequestedDate")) %></td>
                        <td data-label="Time"><%#: Eval("TimeRange") %></td>
                        <td data-label="Status">
                            <tc:StatusBadge ID="badge" runat="server" Status='<%# Eval("Status").ToString() %>' />
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlNoRequests" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">No requests match this filter</p>
            </asp:Panel>
        </div>
    </div>

    <div class="panel">
        <div class="panel__header"><h2 class="panel__title">All bookings</h2></div>
        <div class="table-responsive">
            <asp:Repeater ID="rptBookings" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Every booking on the platform</caption>
                        <thead><tr>
                            <th scope="col" class="numeric">ID</th>
                            <th scope="col">Student</th>
                            <th scope="col">Tutor</th>
                            <th scope="col">Subject</th>
                            <th scope="col">Date</th>
                            <th scope="col">Time</th>
                            <th scope="col">Status</th>
                            <th scope="col">Cancellation</th>
                            <th scope="col">Reviewed</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="ID" class="numeric"><%#: Eval("BookingId") %></td>
                        <td data-label="Student"><%#: Eval("StudentName") %></td>
                        <td data-label="Tutor"><%#: Eval("TutorName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Date"><%# GetDate(Eval("BookingDate")) %></td>
                        <td data-label="Time"><%#: Eval("TimeRange") %></td>
                        <td data-label="Status">
                            <tc:StatusBadge ID="bookingBadge" runat="server" Status='<%# Eval("Status").ToString() %>' />
                        </td>
                        <td data-label="Cancellation"><%#: DescribeCancellation(Container.DataItem) %></td>
                        <td data-label="Reviewed"><%# GetYesNo(Eval("HasReview")) %></td>
                        <td data-label="Action" class="col-action">
                            <asp:LinkButton ID="btnMove" runat="server" CssClass="btn btn--outline btn--sm"
                                Visible='<%# IsScheduled(Eval("Status")) %>'
                                CommandArgument='<%# Eval("BookingId") %>'
                                OnCommand="Move_Command">Reschedule</asp:LinkButton>
                            <asp:LinkButton ID="btnCancel" runat="server" CssClass="btn btn--danger btn--sm"
                                Visible='<%# IsScheduled(Eval("Status")) %>'
                                CommandArgument='<%# Eval("BookingId") %>'
                                OnCommand="Cancel_Command">Cancel</asp:LinkButton>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlNoBookings" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">No bookings yet</p>
            </asp:Panel>
        </div>
    </div>

    <%-- The reason is mandatory here as it is everywhere else. An administrator cancelling
         someone else's session without saying why is the least defensible version of it. --%>
    <asp:Panel ID="pnlCancel" runat="server" CssClass="form-card" Visible="false"
               style="margin-top:24px" DefaultButton="btnConfirmCancel">
        <h2 class="section-title">Cancel booking <asp:Literal ID="litCancelId" runat="server" Mode="Encode" /></h2>
        <p class="form-hint">Both the student and the tutor are told, and the action is logged.</p>

        <div class="field">
            <asp:Label ID="lblReason" runat="server" AssociatedControlID="ddlReason">Reason</asp:Label>
            <div class="field__control">
                <asp:DropDownList ID="ddlReason" runat="server"
                                  DataTextField="Value" DataValueField="Key" />
            </div>
        </div>

        <div class="field">
            <asp:Label ID="lblNote" runat="server" AssociatedControlID="txtNote">Note (optional)</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtNote" runat="server" TextMode="MultiLine" Rows="3" MaxLength="200" />
            </div>
        </div>

        <div class="form-actions">
            <asp:Button ID="btnConfirmCancel" runat="server" Text="Confirm cancellation"
                        CssClass="btn btn--danger" />
            <asp:Button ID="btnAbandon" runat="server" Text="Close" CssClass="btn btn--outline"
                        CausesValidation="false" />
        </div>
    </asp:Panel>

    <%-- Moving a session neither party asked to move. Audited and announced to both of them
         for exactly that reason. --%>
    <asp:Panel ID="pnlMove" runat="server" CssClass="form-card" Visible="false"
               style="margin-top:24px" DefaultButton="btnConfirmMove">
        <h2 class="section-title">Reschedule booking <asp:Literal ID="litMoveId" runat="server" Mode="Encode" /></h2>
        <p class="form-hint">
            The usual route is the student proposing and the tutor agreeing. Use this when
            that is not possible. Both people are told, and the change is logged.
        </p>

        <div class="form-grid">
            <div class="field">
                <asp:Label ID="lblMoveDate" runat="server" AssociatedControlID="txtMoveDate">New date</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtMoveDate" runat="server" TextMode="Date" />
                </div>
            </div>
            <div class="field">
                <asp:Label ID="lblMoveStart" runat="server" AssociatedControlID="txtMoveStart">Start</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtMoveStart" runat="server" TextMode="Time" />
                </div>
            </div>
            <div class="field">
                <asp:Label ID="lblMoveEnd" runat="server" AssociatedControlID="txtMoveEnd">End</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtMoveEnd" runat="server" TextMode="Time" />
                </div>
            </div>
        </div>

        <div class="field">
            <asp:Label ID="lblMoveReason" runat="server" AssociatedControlID="txtMoveReason">
                Reason (required)</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtMoveReason" runat="server" TextMode="MultiLine" Rows="3" MaxLength="200" />
            </div>
        </div>

        <div class="form-actions">
            <asp:Button ID="btnConfirmMove" runat="server" Text="Move the session"
                        CssClass="btn btn--primary" />
            <asp:Button ID="btnAbandonMove" runat="server" Text="Close" CssClass="btn btn--outline"
                        CausesValidation="false" />
        </div>
    </asp:Panel>

</asp:Content>
