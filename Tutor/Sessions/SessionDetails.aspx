<%@ Page Title="Session Details" Language="VB" MasterPageFile="~/MasterPages/Tutor.Master"
    AutoEventWireup="false" CodeBehind="SessionDetails.aspx.vb"
    Inherits="TutorConnect.TutorArea.SessionDetails" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>
<%@ Register TagPrefix="tc" TagName="StatusBadge" Src="~/Controls/StatusBadge.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Session Details - TutorConnect</asp:Content>

<asp:Content ID="H" ContentPlaceHolderID="HeadContent" runat="server">
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/reviews.css") %>" />
</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <asp:Panel ID="pnlDetail" runat="server">

        <div class="form-card" style="margin-bottom:24px">
            <dl class="fact-list">
                <div class="fact-list__row"><dt>Booking ID</dt><dd><asp:Literal ID="litId" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Student</dt><dd><asp:Literal ID="litStudent" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Subject</dt><dd><asp:Literal ID="litSubject" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Date</dt><dd><asp:Literal ID="litDate" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Time</dt><dd><asp:Literal ID="litTime" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Status</dt><dd><tc:StatusBadge ID="badge" runat="server" /></dd></div>
            </dl>

            <div class="form-actions">
                <%-- BR-23: shown only once the session has actually ended. --%>
                <asp:Button ID="btnComplete" runat="server" Text="Mark Completed"
                            CssClass="btn btn--success" Visible="false" />
                <asp:HyperLink ID="lnkBack" runat="server" NavigateUrl="~/Tutor/Sessions/MySessions.aspx"
                               CssClass="btn btn--outline">Back to sessions</asp:HyperLink>
            </div>
        </div>

        <asp:Panel ID="pnlReview" runat="server" CssClass="panel" Visible="false">
            <div class="panel__header"><h2 class="panel__title">Student review</h2></div>
            <div class="form-card">
                <dl class="fact-list">
                    <div class="fact-list__row">
                        <dt>Rating</dt>
                        <dd><asp:Literal ID="litStars" runat="server" /></dd>
                    </div>
                    <div class="fact-list__row">
                        <dt>Comment</dt>
                        <%-- A student's words: encoded, never rendered as markup (SEC-09). --%>
                        <dd><asp:Literal ID="litComment" runat="server" Mode="Encode" /></dd>
                    </div>
                    <div class="fact-list__row">
                        <dt>Submitted</dt>
                        <dd><asp:Literal ID="litReviewed" runat="server" Mode="Encode" /></dd>
                    </div>
                </dl>
                <p class="form-row__hint">A review cannot be edited or removed by a tutor.</p>
            </div>
        </asp:Panel>

        <asp:Panel ID="pnlNoReview" runat="server" CssClass="empty-state" Visible="false">
            <p class="empty-state__title">No review yet</p>
            <p>The student can leave a review once this session is marked completed.</p>
        </asp:Panel>

    </asp:Panel>

</asp:Content>
