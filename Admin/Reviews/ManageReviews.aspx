<%@ Page Title="Reviews" Language="VB" MasterPageFile="~/MasterPages/Admin.Master"
    AutoEventWireup="false" CodeBehind="ManageReviews.aspx.vb"
    Inherits="TutorConnect.AdminArea.ManageReviews" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Reviews - TutorConnect</asp:Content>

<asp:Content ID="H" ContentPlaceHolderID="HeadContent" runat="server">
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/reviews.css") %>" />
</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <%-- Second stage of hiding. A reason is required, so the panel exists rather than a
         one-click hide: "hidden by an administrator" with no explanation is not an audit
         trail, and the student is shown this text. --%>
    <asp:Panel ID="pnlHide" runat="server" CssClass="panel" Visible="false"
               DefaultButton="btnConfirmHide" style="margin-bottom:24px">
        <div class="panel__header">
            <h2 class="panel__title">Hide review <asp:Literal ID="litHideId" runat="server" Mode="Encode" /></h2>
        </div>
        <div class="form-card">
            <div class="form-row">
                <asp:Label ID="lblReason" runat="server" AssociatedControlID="txtReason">Reason (required)</asp:Label>
                <asp:TextBox ID="txtReason" runat="server" TextMode="MultiLine" MaxLength="200"
                             placeholder="For example: abusive language." />
                <p class="form-row__hint">
                    200 characters maximum. The student who wrote the review will see this.
                </p>
            </div>
            <div class="form-actions">
                <asp:Button ID="btnConfirmHide" runat="server" Text="Hide Review" CssClass="btn btn--danger" />
                <asp:Button ID="btnCancelHide" runat="server" Text="Cancel" CssClass="btn btn--ghost"
                            CausesValidation="false" />
            </div>
        </div>
    </asp:Panel>

    <div class="panel">
        <div class="panel__header"><h2 class="panel__title">All reviews</h2></div>

        <div class="table-responsive">
            <asp:Repeater ID="rptReviews" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Every review, including hidden ones</caption>
                        <thead><tr>
                            <th scope="col">Student</th>
                            <th scope="col">Tutor</th>
                            <th scope="col">Subject</th>
                            <th scope="col">Rating</th>
                            <th scope="col">Comment</th>
                            <th scope="col">Status</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Student"><%#: Eval("StudentName") %></td>
                        <td data-label="Tutor"><%#: Eval("TutorName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Rating"><%# GetStars(Eval("Rating")) %></td>
                        <td data-label="Comment"><%#: Eval("Comment") %></td>
                        <td data-label="Status"><%# GetStatus(Container.DataItem) %></td>
                        <td data-label="Action" class="col-action">
                            <asp:LinkButton ID="btnHide" runat="server" CssClass="btn btn--danger btn--sm"
                                Visible='<%# Not CBool(Eval("IsHidden")) %>'
                                CommandArgument='<%# Eval("ReviewId") %>' OnCommand="Hide_Command">Hide</asp:LinkButton>
                            <asp:LinkButton ID="btnUnhide" runat="server" CssClass="btn btn--outline btn--sm"
                                Visible='<%# CBool(Eval("IsHidden")) %>'
                                CommandArgument='<%# Eval("ReviewId") %>' OnCommand="Unhide_Command">Restore</asp:LinkButton>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">No reviews yet</p>
                <p>Reviews appear once students rate completed sessions.</p>
            </asp:Panel>
        </div>
    </div>

    <p class="form-row__hint">
        Reviews are never deleted. Hiding removes one from the tutor's profile and from
        their rating average while keeping the record.
    </p>

</asp:Content>
