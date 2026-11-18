<%@ Page Title="My Requests" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="MyRequests.aspx.vb"
    Inherits="TutorConnect.StudentArea.MyRequests" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>
<%@ Register TagPrefix="tc" TagName="StatusBadge" Src="~/Controls/StatusBadge.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">My Requests - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">My Tutor Requests</h2>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptRequests" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Session requests you have sent</caption>
                        <thead>
                            <tr>
                                <th scope="col" class="numeric">Request ID</th>
                                <th scope="col">Tutor</th>
                                <th scope="col">Subject</th>
                                <th scope="col">Level</th>
                                <th scope="col">Date Requested</th>
                                <th scope="col">Status</th>
                                <th scope="col" class="col-action">Action</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Request ID" class="numeric"><%#: Eval("RequestId") %></td>
                        <td data-label="Tutor"><%#: Eval("TutorName") %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Level"><%#: Eval("StudyLevelName") %></td>
                        <%-- The mockup's "Date Requested" is when the request was made, not
                             the slot itself. --%>
                        <td data-label="Date Requested"><%# GetDate(Eval("CreatedAt")) %></td>
                        <td data-label="Status">
                            <tc:StatusBadge ID="badge" runat="server" Status='<%# Eval("Status").ToString() %>' />
                        </td>
                        <td data-label="Action" class="col-action">
                            <%-- BR-13: only a pending request can be withdrawn. Anything else
                                 shows an em dash, as in the design. --%>
                            <asp:LinkButton ID="btnCancel" runat="server" CssClass="btn btn--danger btn--sm"
                                Visible='<%# Eval("Status").ToString() = "Pending" %>'
                                CommandArgument='<%# Eval("RequestId") %>' OnCommand="Cancel_Command"
                                OnClientClick="return confirm('Withdraw this request?');">Cancel Request</asp:LinkButton>
                            <asp:Label ID="lblNoAction" runat="server" CssClass="no-action" Text="&#8212;"
                                       Visible='<%# Eval("Status").ToString() <> "Pending" %>' />
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                        </tbody>
                    </table>
                </FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">You have not requested any sessions yet</p>
                <p>Find a tutor and request a time that suits you.</p>
                <asp:HyperLink ID="lnkFind" runat="server" NavigateUrl="~/Student/Tutors/FindTutor.aspx"
                               CssClass="btn btn--outline btn--sm">Find a tutor</asp:HyperLink>
            </asp:Panel>
        </div>

        <div class="panel__footer">
            <asp:Button ID="btnRefresh" runat="server" Text="Refresh" CssClass="btn btn--outline btn--sm" />
        </div>
    </div>

</asp:Content>
