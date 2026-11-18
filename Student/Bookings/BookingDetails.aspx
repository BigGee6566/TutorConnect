<%@ Page Title="Booking Details" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="BookingDetails.aspx.vb"
    Inherits="TutorConnect.StudentArea.BookingDetails" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>
<%@ Register TagPrefix="tc" TagName="StatusBadge" Src="~/Controls/StatusBadge.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Booking Details - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <asp:Panel ID="pnlDetail" runat="server" CssClass="form-card">
        <dl class="fact-list">
            <div class="fact-list__row"><dt>Booking ID</dt><dd><asp:Literal ID="litId" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Tutor</dt><dd><asp:Literal ID="litTutor" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Subject</dt><dd><asp:Literal ID="litSubject" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Date</dt><dd><asp:Literal ID="litDate" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Time</dt><dd><asp:Literal ID="litTime" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Rate</dt><dd><asp:Literal ID="litRate" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row">
                <dt>Status</dt>
                <dd><tc:StatusBadge ID="badge" runat="server" /></dd>
            </div>
            <div class="fact-list__row"><dt>Booked on</dt><dd><asp:Literal ID="litCreated" runat="server" Mode="Encode" /></dd></div>

            <%-- Cancellation history. Hidden entirely on a session that is still live, so a
                 scheduled booking does not show four empty rows. --%>
            <asp:PlaceHolder ID="phCancelled" runat="server" Visible="false">
                <div class="fact-list__row"><dt>Cancelled on</dt><dd><asp:Literal ID="litCancelledAt" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Cancelled by</dt><dd><asp:Literal ID="litCancelledBy" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Reason</dt><dd><asp:Literal ID="litCancelReason" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Note</dt><dd><asp:Literal ID="litCancelNote" runat="server" Mode="Encode" /></dd></div>
            </asp:PlaceHolder>
        </dl>

        <%-- The state of any postponement proposal on this booking. --%>
        <asp:Panel ID="pnlPostponeState" runat="server" CssClass="form-card form-card--inset" Visible="false">
            <h2 class="section-title">Postponement</h2>
            <dl class="fact-list">
                <div class="fact-list__row"><dt>Requested</dt><dd><asp:Literal ID="litProposed" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Instead of</dt><dd><asp:Literal ID="litOriginal" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Reason</dt><dd><asp:Literal ID="litPostponeReason" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>State</dt><dd><asp:Literal ID="litPostponeStatus" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Tutor's response</dt><dd><asp:Literal ID="litTutorResponse" runat="server" Mode="Encode" /></dd></div>
            </dl>

            <div class="form-actions">
                <asp:Button ID="btnWithdraw" runat="server" Text="Withdraw this request"
                            CssClass="btn btn--outline" CausesValidation="false" />
            </div>
        </asp:Panel>

        <div class="form-actions">
            <%-- Appears only when the session is completed and has no review yet
                 (BR-16, BR-17). --%>
            <asp:HyperLink ID="lnkWriteReview" runat="server" CssClass="btn btn--success"
                           Visible="false">Write Review</asp:HyperLink>

            <%-- Both appear only on a session that is still scheduled. The server checks
                 that again on postback: hiding a button is presentation, not a rule. --%>
            <asp:Button ID="btnShowCancel" runat="server" Text="Cancel session"
                        CssClass="btn btn--danger" Visible="false" CausesValidation="false" />
            <asp:Button ID="btnShowPostpone" runat="server" Text="Ask to postpone"
                        CssClass="btn btn--outline" Visible="false" CausesValidation="false" />

            <asp:HyperLink ID="lnkBack" runat="server" NavigateUrl="~/Student/Bookings/MyBookings.aspx"
                           CssClass="btn btn--outline">Back to bookings</asp:HyperLink>
        </div>
    </asp:Panel>

    <%-- ------------------------------------------------------------ Cancel --%>
    <asp:Panel ID="pnlCancel" runat="server" CssClass="form-card" Visible="false"
               DefaultButton="btnConfirmCancel">
        <h2 class="section-title">Cancel this session</h2>
        <p class="form-hint">
            The tutor is told straight away, and the slot goes back on their calendar.
            This cannot be undone.
        </p>

        <div class="field">
            <asp:Label ID="lblCancelReason" runat="server" AssociatedControlID="ddlCancelReason">
                Why are you cancelling?</asp:Label>
            <div class="field__control">
                <asp:DropDownList ID="ddlCancelReason" runat="server"
                                  DataTextField="Value" DataValueField="Key" />
            </div>
        </div>

        <div class="field">
            <asp:Label ID="lblCancelNote" runat="server" AssociatedControlID="txtCancelNote">
                Anything to add? (optional)</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtCancelNote" runat="server" TextMode="MultiLine" Rows="3"
                             MaxLength="200" placeholder="Up to 200 characters" />
            </div>
        </div>

        <div class="form-actions">
            <asp:Button ID="btnConfirmCancel" runat="server" Text="Confirm cancellation"
                        CssClass="btn btn--danger" />
            <asp:Button ID="btnAbandonCancel" runat="server" Text="Keep the session"
                        CssClass="btn btn--outline" CausesValidation="false" />
        </div>
    </asp:Panel>

    <%-- ---------------------------------------------------------- Postpone --%>
    <asp:Panel ID="pnlPostpone" runat="server" CssClass="form-card" Visible="false"
               DefaultButton="btnConfirmPostpone">
        <h2 class="section-title">Ask to move this session</h2>
        <p class="form-hint">
            Your session stays exactly as it is until the tutor agrees. They can accept or
            decline, and you will be told either way.
        </p>

        <div class="form-grid">
            <div class="field">
                <asp:Label ID="lblNewDate" runat="server" AssociatedControlID="txtNewDate">New date</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtNewDate" runat="server" TextMode="Date" />
                </div>
            </div>
            <div class="field">
                <asp:Label ID="lblNewStart" runat="server" AssociatedControlID="txtNewStart">Start</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtNewStart" runat="server" TextMode="Time" />
                </div>
            </div>
            <div class="field">
                <asp:Label ID="lblNewEnd" runat="server" AssociatedControlID="txtNewEnd">End</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtNewEnd" runat="server" TextMode="Time" />
                </div>
            </div>
        </div>

        <div class="field">
            <asp:Label ID="lblPostponeReason" runat="server" AssociatedControlID="ddlPostponeReason">
                Why do you need to move it?</asp:Label>
            <div class="field__control">
                <asp:DropDownList ID="ddlPostponeReason" runat="server"
                                  DataTextField="Value" DataValueField="Key" />
            </div>
        </div>

        <div class="field">
            <asp:Label ID="lblPostponeNote" runat="server" AssociatedControlID="txtPostponeNote">
                Anything to add? (optional)</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtPostponeNote" runat="server" TextMode="MultiLine" Rows="3"
                             MaxLength="200" placeholder="Up to 200 characters" />
            </div>
        </div>

        <div class="form-actions">
            <asp:Button ID="btnConfirmPostpone" runat="server" Text="Send the request"
                        CssClass="btn btn--primary" />
            <asp:Button ID="btnAbandonPostpone" runat="server" Text="Leave it as it is"
                        CssClass="btn btn--outline" CausesValidation="false" />
        </div>
    </asp:Panel>

</asp:Content>
