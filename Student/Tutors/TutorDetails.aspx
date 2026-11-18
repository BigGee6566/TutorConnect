<%@ Page Title="Tutor Details" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="TutorDetails.aspx.vb"
    Inherits="TutorConnect.StudentArea.TutorDetails" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Tutor Details - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <asp:Panel ID="pnlDetail" runat="server" CssClass="detail-grid">

        <div class="panel detail-grid__aside">
            <div class="tutor-identity">
                <span class="tutor-identity__avatar" aria-hidden="true"><asp:Literal ID="litInitials" runat="server" Mode="Encode" /></span>
                <h2 class="tutor-identity__name"><asp:Literal ID="litName" runat="server" Mode="Encode" /></h2>
                <p class="tutor-identity__qualification"><asp:Literal ID="litQualification" runat="server" Mode="Encode" /></p>
            </div>

            <dl class="fact-list">
                <div class="fact-list__row">
                    <dt>Experience</dt>
                    <dd><asp:Literal ID="litExperience" runat="server" Mode="Encode" /></dd>
                </div>
                <div class="fact-list__row">
                    <dt>Rate/Session</dt>
                    <dd><asp:Literal ID="litRate" runat="server" Mode="Encode" /></dd>
                </div>
                <div class="fact-list__row">
                    <dt>Rating</dt>
                    <dd><asp:Literal ID="litRating" runat="server" Mode="Encode" /></dd>
                </div>
            </dl>
        </div>

        <div class="panel detail-grid__main">
            <div class="panel__header">
                <h2 class="panel__title"><asp:Literal ID="litSubjectLevel" runat="server" Mode="Encode" /></h2>
            </div>

            <div class="detail-body">
                <h3 class="detail-body__heading">About Tutor</h3>
                <%-- Free text written by the tutor: encoded, never rendered as markup. --%>
                <p class="detail-body__bio"><asp:Literal ID="litBio" runat="server" Mode="Encode" /></p>

                <h3 class="detail-body__heading">Availability</h3>

                <div class="table-responsive">
                    <asp:Repeater ID="rptSlots" runat="server">
                        <HeaderTemplate>
                            <table class="data-table">
                                <caption class="sr-only">Future available time slots for this tutor</caption>
                                <thead>
                                    <tr>
                                        <th scope="col">Date</th>
                                        <th scope="col">Start Time</th>
                                        <th scope="col">End Time</th>
                                        <th scope="col" class="col-action">Action</th>
                                    </tr>
                                </thead>
                                <tbody>
                        </HeaderTemplate>
                        <ItemTemplate>
                            <tr>
                                <td data-label="Date"><%# GetDate(Eval("AvailabilityDate")) %></td>
                                <td data-label="Start Time"><%# GetTime(Eval("StartTime")) %></td>
                                <td data-label="End Time"><%# GetTime(Eval("EndTime")) %></td>
                                <td data-label="Action" class="col-action">
                                    <a class="btn btn--success btn--sm" href="<%# GetRequestUrl(Eval("AvailabilityId")) %>">Request Session</a>
                                </td>
                            </tr>
                        </ItemTemplate>
                        <FooterTemplate>
                                </tbody>
                            </table>
                        </FooterTemplate>
                    </asp:Repeater>

                    <asp:Panel ID="pnlNoSlots" runat="server" CssClass="empty-state" Visible="false">
                        <p class="empty-state__title">No available times right now</p>
                        <p>This tutor has not published any future slots yet. Try again later.</p>
                    </asp:Panel>
                </div>
            </div>
        </div>

    </asp:Panel>

    <div class="form-actions">
        <asp:HyperLink ID="lnkBack" runat="server" NavigateUrl="~/Student/Tutors/FindTutor.aspx"
                       CssClass="btn btn--outline">Back to search</asp:HyperLink>
    </div>

</asp:Content>
