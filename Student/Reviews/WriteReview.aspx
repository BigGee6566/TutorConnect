<%@ Page Title="Write a Review" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="WriteReview.aspx.vb"
    Inherits="TutorConnect.StudentArea.WriteReview" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Write a Review - TutorConnect</asp:Content>

<asp:Content ID="H" ContentPlaceHolderID="HeadContent" runat="server">
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/reviews.css") %>" />
</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <asp:Panel ID="pnlForm" runat="server" CssClass="panel">
        <div class="panel__header">
            <h2 class="panel__title">Write a Review</h2>
        </div>

        <div class="form-card">
            <dl class="fact-list">
                <div class="fact-list__row"><dt>Tutor</dt><dd><asp:Literal ID="litTutor" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Subject</dt><dd><asp:Literal ID="litSubject" runat="server" Mode="Encode" /></dd></div>
                <div class="fact-list__row"><dt>Session Date</dt><dd><asp:Literal ID="litSession" runat="server" Mode="Encode" /></dd></div>
            </dl>

            <div class="form-row">
                <span class="field-legend" id="ratingLabel">Rating</span>

                <%-- Radio inputs styled as stars. They stay real radios, so the control is
                     keyboard-reachable and works with JavaScript disabled; reviews.js only
                     adds the hover preview and the live label. --%>
                <div class="star-rating" role="radiogroup" aria-labelledby="ratingLabel">
                    <asp:RadioButtonList ID="rblRating" runat="server" RepeatLayout="Flow"
                                         RepeatDirection="Horizontal" CssClass="star-rating__list">
                        <asp:ListItem Value="1" Text="1 Poor" />
                        <asp:ListItem Value="2" Text="2 Fair" />
                        <asp:ListItem Value="3" Text="3 Good" />
                        <asp:ListItem Value="4" Text="4 Very good" />
                        <asp:ListItem Value="5" Text="5 Excellent" />
                    </asp:RadioButtonList>
                    <p class="star-rating__label" data-rating-label aria-live="polite"></p>
                </div>
            </div>

            <div class="form-row">
                <asp:Label ID="lblComment" runat="server" AssociatedControlID="txtComment">Comment</asp:Label>
                <asp:TextBox ID="txtComment" runat="server" TextMode="MultiLine" MaxLength="500"
                             data-counted="true"
                             placeholder="What went well? What could be better?" />
                <p class="form-row__hint">
                    <span data-counter>0</span> of 500 characters.
                </p>
            </div>

            <div class="form-actions">
                <asp:Button ID="btnSubmit" runat="server" Text="Submit Review" CssClass="btn btn--success" />
                <asp:HyperLink ID="lnkCancel" runat="server" NavigateUrl="~/Student/Bookings/MyBookings.aspx"
                               CssClass="btn btn--ghost">Cancel</asp:HyperLink>
            </div>
        </div>
    </asp:Panel>

</asp:Content>

<asp:Content ID="S" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="<%: ResolveUrl("~/Scripts/reviews.js") %>"></script>
</asp:Content>
