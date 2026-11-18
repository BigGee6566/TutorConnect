<%@ Page Title="My Reviews" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="MyReviews.aspx.vb"
    Inherits="TutorConnect.StudentArea.MyReviews" Async="true" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">My Reviews - TutorConnect</asp:Content>

<asp:Content ID="H" ContentPlaceHolderID="HeadContent" runat="server">
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/reviews.css") %>" />
</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">Reviews I have written</h2>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptReviews" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Reviews you have submitted</caption>
                        <thead>
                            <tr>
                                <th scope="col">Tutor</th>
                                <th scope="col">Subject</th>
                                <th scope="col">Session Date</th>
                                <th scope="col">Rating</th>
                                <th scope="col">Comment</th>
                                <th scope="col">Submitted</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Tutor"><%#: Eval("TutorName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Session Date"><%# GetDate(Eval("SessionDate")) %></td>
                        <td data-label="Rating"><%# GetStars(Eval("Rating")) %></td>
                        <td data-label="Comment">
                            <%#: Eval("Comment") %>
                            <%-- A hidden review is still shown to its author, with the
                                 reason. Removing someone's own words without telling them
                                 would not be honest. --%>
                            <%# GetHiddenNotice(Container.DataItem) %>
                        </td>
                        <td data-label="Submitted"><%# GetCreated(Eval("CreatedAt")) %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                        </tbody>
                    </table>
                </FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">You have not written any reviews yet</p>
                <p>Once a tutor marks a session completed you can rate it from My Bookings.</p>
            </asp:Panel>
        </div>
    </div>

</asp:Content>
