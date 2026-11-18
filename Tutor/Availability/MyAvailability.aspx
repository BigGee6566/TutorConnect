<%@ Page Title="My Availability" Language="VB" MasterPageFile="~/MasterPages/Tutor.Master"
    AutoEventWireup="false" CodeBehind="MyAvailability.aspx.vb"
    Inherits="TutorConnect.TutorArea.MyAvailability" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">My Availability - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="panel" style="margin-bottom:24px">
        <div class="panel__header"><h2 class="panel__title">Add Availability</h2></div>
        <div class="form-card">
            <div class="filter-bar">
                <div class="form-row">
                    <asp:Label ID="lblSubject" runat="server" AssociatedControlID="ddlSubject">Subject</asp:Label>
                    <asp:DropDownList ID="ddlSubject" runat="server" DataValueField="TutorSubjectId" DataTextField="Display" />
                </div>
                <div class="form-row">
                    <asp:Label ID="lblDate" runat="server" AssociatedControlID="txtDate">Date</asp:Label>
                    <asp:TextBox ID="txtDate" runat="server" TextMode="Date" />
                </div>
                <div class="form-row">
                    <asp:Label ID="lblStart" runat="server" AssociatedControlID="txtStart">Start Time</asp:Label>
                    <asp:TextBox ID="txtStart" runat="server" TextMode="Time" />
                </div>
                <div class="form-row">
                    <asp:Label ID="lblEnd" runat="server" AssociatedControlID="txtEnd">End Time</asp:Label>
                    <asp:TextBox ID="txtEnd" runat="server" TextMode="Time" />
                </div>
                <asp:Button ID="btnAdd" runat="server" Text="Add Availability" CssClass="btn btn--success" />
            </div>
        </div>
    </div>

    <div class="panel">
        <div class="panel__header"><h2 class="panel__title">My Availability</h2></div>
        <div class="table-responsive">
            <asp:Repeater ID="rptSlots" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Time slots you have published</caption>
                        <thead><tr>
                            <th scope="col">Date</th>
                            <th scope="col">Start Time</th>
                            <th scope="col">End Time</th>
                            <th scope="col">Subject</th>
                            <th scope="col">Booked</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Date"><%# GetDate(Eval("AvailabilityDate")) %></td>
                        <td data-label="Start Time"><%# GetTime(Eval("StartTime")) %></td>
                        <td data-label="End Time"><%# GetTime(Eval("EndTime")) %></td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Booked"><%# GetBooked(Eval("IsBooked")) %></td>
                        <td data-label="Action" class="col-action">
                            <asp:LinkButton ID="btnDelete" runat="server" CssClass="btn btn--danger btn--sm"
                                CommandArgument='<%# Eval("AvailabilityId") %>' OnCommand="Delete_Command"
                                OnClientClick="return confirm('Delete this availability slot?');">Delete</asp:LinkButton>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">You have not published any times yet</p>
                <p>Students can only request a session inside a slot you publish.</p>
            </asp:Panel>
        </div>

        <div class="panel__footer">
            <asp:Button ID="btnRefresh" runat="server" Text="Refresh" CssClass="btn btn--outline btn--sm" />
        </div>
    </div>

</asp:Content>
