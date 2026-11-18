<%@ Page Title="Dashboard" Language="VB" MasterPageFile="~/MasterPages/Tutor.Master"
    AutoEventWireup="false" CodeBehind="Dashboard.aspx.vb"
    Inherits="TutorConnect.TutorArea.Dashboard" Async="true" %>

<%@ Register TagPrefix="tc" TagName="DashboardCard" Src="~/Controls/DashboardCard.ascx" %>

<asp:Content ID="DashTitle" ContentPlaceHolderID="TitleContent" runat="server">Dashboard - TutorConnect</asp:Content>

<asp:Content ID="DashBody" ContentPlaceHolderID="MainContent" runat="server">

    <div class="card-grid">

        <tc:DashboardCard ID="cardProfile" runat="server"
            Title="My Profile"
            Description="View and update your profile"
            NavigateUrl="~/Tutor/Profile/MyProfile.aspx"
            Accent="Blue" />

        <tc:DashboardCard ID="cardSubjects" runat="server"
            Title="My Subjects"
            Description="Manage your subjects"
            NavigateUrl="~/Tutor/Subjects/MySubjects.aspx"
            Accent="Green" />

        <tc:DashboardCard ID="cardAvailability" runat="server"
            Title="My Availability"
            Description="Set your available times"
            NavigateUrl="~/Tutor/Availability/MyAvailability.aspx"
            Accent="Blue" />

        <%-- Count is set in code-behind from the pending-request total. --%>
        <tc:DashboardCard ID="cardRequests" runat="server"
            Title="Student Requests"
            Description="View and respond to requests"
            NavigateUrl="~/Tutor/Requests/StudentRequests.aspx"
            Accent="Blue" />

        <tc:DashboardCard ID="cardSessions" runat="server"
            Title="My Sessions"
            Description="View your scheduled sessions"
            NavigateUrl="~/Tutor/Sessions/MySessions.aspx"
            Accent="Blue" />

    </div>

</asp:Content>
