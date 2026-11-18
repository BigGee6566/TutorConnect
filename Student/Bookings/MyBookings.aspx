<%@ Page Title="My Bookings" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="MyBookings.aspx.vb"
    Inherits="TutorConnect.StudentArea.MyBookings" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>
<%@ Register TagPrefix="tc" TagName="StatusBadge" Src="~/Controls/StatusBadge.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">My Bookings - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">My Bookings</h2>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptBookings" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Your scheduled and completed sessions</caption>
                        <thead>
                            <tr>
                                <%-- The design labels this "Reoking ID"; that is a typo in the
                                     mockup and is rendered correctly here. --%>
                                <th scope="col" class="numeric">Booking ID</th>
                                <th scope="col">Tutor</th>
                                <th scope="col">Subject</th>
                                <th scope="col">Date</th>
                                <th scope="col">Time</th>
                                <th scope="col">Status</th>
                                <th scope="col" class="col-action">Action</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Booking ID" class="numeric"><%#: Eval("BookingId") %></td>
                        <td data-label="Tutor"><%#: Eval("TutorName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Date"><%# GetDate(Eval("BookingDate")) %></td>
                        <td data-label="Time"><%#: Eval("TimeRange") %></td>
                        <td data-label="Status">
                            <tc:StatusBadge ID="badge" runat="server" Status='<%# Eval("Status").ToString() %>' />
                        </td>
                        <%-- Cancel and Postpone are links into the detail page with the right
                             panel already open, rather than a second copy of the reason form
                             inside this repeater. One implementation, one place to check. --%>
                        <td data-label="Action" class="col-action">
                            <a class="btn btn--outline btn--sm" href="<%# GetDetailUrl(Eval("BookingId")) %>">View</a>
                            <asp:PlaceHolder runat="server" Visible='<%# IsScheduled(Eval("Status")) %>'>
                                <a class="btn btn--outline btn--sm"
                                   href="<%# GetActionUrl(Eval("BookingId"), "postpone") %>">Postpone</a>
                                <a class="btn btn--danger btn--sm"
                                   href="<%# GetActionUrl(Eval("BookingId"), "cancel") %>">Cancel</a>
                            </asp:PlaceHolder>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                        </tbody>
                    </table>
                </FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">You have no bookings yet</p>
                <p>A booking appears here once a tutor accepts one of your requests.</p>
                <asp:HyperLink ID="lnkRequests" runat="server" NavigateUrl="~/Student/Requests/MyRequests.aspx"
                               CssClass="btn btn--outline btn--sm">See my requests</asp:HyperLink>
            </asp:Panel>
        </div>

        <div class="panel__footer">
            <asp:Button ID="btnRefresh" runat="server" Text="Refresh" CssClass="btn btn--outline btn--sm" />
        </div>
    </div>

</asp:Content>
