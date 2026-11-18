<%@ Page Title="My Sessions" Language="VB" MasterPageFile="~/MasterPages/Tutor.Master"
    AutoEventWireup="false" CodeBehind="MySessions.aspx.vb"
    Inherits="TutorConnect.TutorArea.MySessions" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>
<%@ Register TagPrefix="tc" TagName="StatusBadge" Src="~/Controls/StatusBadge.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">My Sessions - TutorConnect</asp:Content>

<asp:Content ID="H" ContentPlaceHolderID="HeadContent" runat="server">
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/reviews.css") %>" />
</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <%-- ------------------------------------------------- Postponements (12.5)
         First on the page when there is something waiting: these are the only rows
         on this screen that need the tutor to do something. --%>
    <asp:Panel ID="pnlPostponements" runat="server" CssClass="panel" Visible="false"
               style="margin-bottom:24px">
        <div class="panel__header"><h2 class="panel__title">Postponement requests</h2></div>

        <div class="table-responsive">
            <asp:Repeater ID="rptPostponements" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Requests from students to move a session</caption>
                        <thead><tr>
                            <th scope="col">Student</th>
                            <th scope="col">Subject</th>
                            <th scope="col">Agreed time</th>
                            <th scope="col">Asked for</th>
                            <th scope="col">Reason</th>
                            <th scope="col">Status</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Student"><%#: Eval("StudentName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Agreed time"><%#: Eval("OriginalRange") %></td>
                        <td data-label="Asked for"><%#: Eval("ProposedRange") %></td>
                        <td data-label="Reason"><%# DescribeReason(Eval("ReasonCategory")) %></td>
                        <td data-label="Status"><%#: DescribeStatus(Container.DataItem) %></td>
                        <td data-label="Action" class="col-action">
                            <%-- Only a proposal still waiting can be answered; the service
                                 checks that again, because a hidden button can still be
                                 posted to. --%>
                            <asp:LinkButton ID="btnAnswer" runat="server" CssClass="btn btn--outline btn--sm"
                                Visible='<%# CBool(Eval("IsPending")) %>'
                                CommandArgument='<%# Eval("PostponementId") %>'
                                OnCommand="Answer_Command">Answer</asp:LinkButton>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
        </div>
    </asp:Panel>

    <%-- The answer form, shown for one proposal at a time. --%>
    <asp:Panel ID="pnlAnswer" runat="server" CssClass="form-card" Visible="false"
               style="margin-bottom:24px" DefaultButton="btnApprove">
        <h2 class="section-title">Answer this request</h2>

        <dl class="fact-list">
            <div class="fact-list__row"><dt>Student</dt><dd><asp:Literal ID="litAnswerStudent" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Agreed time</dt><dd><asp:Literal ID="litAnswerOriginal" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Asked for</dt><dd><asp:Literal ID="litAnswerProposed" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Reason</dt><dd><asp:Literal ID="litAnswerReason" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Their note</dt><dd><asp:Literal ID="litAnswerNote" runat="server" Mode="Encode" /></dd></div>
        </dl>

        <div class="field">
            <asp:Label ID="lblResponse" runat="server" AssociatedControlID="txtResponse">
                Your reply (required if you decline)</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtResponse" runat="server" TextMode="MultiLine" Rows="3"
                             MaxLength="200" placeholder="Up to 200 characters" />
            </div>
        </div>

        <div class="form-actions">
            <asp:Button ID="btnApprove" runat="server" Text="Approve and move the session"
                        CssClass="btn btn--success" />
            <asp:Button ID="btnDecline" runat="server" Text="Decline" CssClass="btn btn--danger" />
            <asp:Button ID="btnCloseAnswer" runat="server" Text="Close" CssClass="btn btn--outline"
                        CausesValidation="false" />
        </div>
    </asp:Panel>

    <%-- The tutor's own cancellation form, shown for one session at a time. --%>
    <asp:Panel ID="pnlCancelSession" runat="server" CssClass="form-card" Visible="false"
               style="margin-bottom:24px" DefaultButton="btnConfirmCancelSession">
        <h2 class="section-title">Cancel session <asp:Literal ID="litCancelId" runat="server" Mode="Encode" /></h2>
        <p class="form-hint">The student is told straight away, with your reason.</p>

        <div class="field">
            <asp:Label ID="lblCancelReason" runat="server" AssociatedControlID="ddlCancelReason">Reason</asp:Label>
            <div class="field__control">
                <asp:DropDownList ID="ddlCancelReason" runat="server"
                                  DataTextField="Value" DataValueField="Key" />
            </div>
        </div>

        <div class="field">
            <asp:Label ID="lblCancelNote" runat="server" AssociatedControlID="txtCancelNote">
                Anything to add? (optional)</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtCancelNote" runat="server" TextMode="MultiLine" Rows="3" MaxLength="200" />
            </div>
        </div>

        <div class="form-actions">
            <asp:Button ID="btnConfirmCancelSession" runat="server" Text="Confirm cancellation"
                        CssClass="btn btn--danger" />
            <asp:Button ID="btnAbandonCancel" runat="server" Text="Keep the session"
                        CssClass="btn btn--outline" CausesValidation="false" />
        </div>
    </asp:Panel>

    <div class="panel" style="margin-bottom:24px">
        <div class="panel__header"><h2 class="panel__title">My Sessions</h2></div>

        <div class="table-responsive">
            <asp:Repeater ID="rptSessions" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Your scheduled and completed sessions</caption>
                        <thead><tr>
                            <th scope="col" class="numeric">Booking ID</th>
                            <th scope="col">Student</th>
                            <th scope="col">Subject</th>
                            <th scope="col">Date</th>
                            <th scope="col">Time</th>
                            <th scope="col">Status</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Booking ID" class="numeric"><%#: Eval("BookingId") %></td>
                        <td data-label="Student"><%#: Eval("StudentName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Date"><%# GetDate(Eval("BookingDate")) %></td>
                        <td data-label="Time"><%#: Eval("TimeRange") %></td>
                        <td data-label="Status">
                            <tc:StatusBadge ID="badge" runat="server" Status='<%# Eval("Status").ToString() %>' />
                        </td>
                        <td data-label="Action" class="col-action">
                            <%-- BR-23: only a Scheduled session whose end time has passed can
                                 be completed, so the button is not shown before then. --%>
                            <asp:LinkButton ID="btnComplete" runat="server" CssClass="btn btn--success btn--sm"
                                Visible='<%# CanComplete(Container.DataItem) %>'
                                CommandArgument='<%# Eval("BookingId") %>' OnCommand="Complete_Command">Mark Completed</asp:LinkButton>
                            <%-- A tutor can cancel a session they can no longer teach. The
                                 student is told, with the reason. --%>
                            <asp:LinkButton ID="btnCancelSession" runat="server" CssClass="btn btn--danger btn--sm"
                                Visible='<%# IsScheduled(Eval("Status")) %>'
                                CommandArgument='<%# Eval("BookingId") %>' OnCommand="Cancel_Command">Cancel</asp:LinkButton>
                            <a class="btn btn--outline btn--sm" href="<%# GetDetailUrl(Eval("BookingId")) %>">View</a>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">No sessions yet</p>
                <p>A session appears here once you accept a student's request.</p>
            </asp:Panel>
        </div>

        <div class="panel__footer">
            <asp:Button ID="btnRefresh" runat="server" Text="Refresh" CssClass="btn btn--outline btn--sm" />
        </div>
    </div>

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">My Reviews</h2>
            <span class="page-subtitle"><asp:Literal ID="litRating" runat="server" Mode="Encode" /></span>
        </div>

        <div class="table-responsive">
            <%-- Read-only. A tutor can never edit or delete a review. --%>
            <asp:Repeater ID="rptReviews" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Reviews students have left for you</caption>
                        <thead><tr>
                            <th scope="col">Student</th>
                            <th scope="col">Subject</th>
                            <th scope="col">Session Date</th>
                            <th scope="col">Rating</th>
                            <th scope="col">Comment</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Student"><%#: Eval("StudentName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Session Date"><%# GetDate(Eval("SessionDate")) %></td>
                        <td data-label="Rating"><%# GetStars(Eval("Rating")) %></td>
                        <td data-label="Comment"><%#: Eval("Comment") %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlNoReviews" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">No reviews yet</p>
                <p>Students can review a session once you mark it completed.</p>
            </asp:Panel>
        </div>
    </div>

</asp:Content>
