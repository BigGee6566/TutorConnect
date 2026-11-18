<%@ Page Title="Dashboard" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="Dashboard.aspx.vb"
    Inherits="TutorConnect.StudentArea.Dashboard" Async="true" %>

<%@ Register TagPrefix="tc" TagName="DashboardCard" Src="~/Controls/DashboardCard.ascx" %>

<asp:Content ID="DashTitle" ContentPlaceHolderID="TitleContent" runat="server">Dashboard - TutorConnect</asp:Content>

<asp:Content ID="DashBody" ContentPlaceHolderID="MainContent" runat="server">

    <%-- Six tiles in a 3x2 grid, as in the design. Each links to its real target;
         the ones whose screens arrive in Phase 5 land on the styled 404 until then. --%>
    <div class="card-grid">

        <tc:DashboardCard ID="cardProfile" runat="server"
            Title="My Profile"
            Description="View and update your profile"
            NavigateUrl="~/Student/Profile/MyProfile.aspx"
            Accent="Blue" />

        <tc:DashboardCard ID="cardSubjects" runat="server"
            Title="My Subjects"
            Description="Manage your preferred subjects"
            NavigateUrl="~/Student/Subjects/MySubjects.aspx"
            Accent="Blue" />

        <tc:DashboardCard ID="cardFindTutor" runat="server"
            Title="Find Tutor"
            Description="Search and find the best tutors"
            NavigateUrl="~/Student/Tutors/FindTutor.aspx"
            Accent="Blue" />

        <tc:DashboardCard ID="cardRequests" runat="server"
            Title="My Requests"
            Description="View your tutor requests"
            NavigateUrl="~/Student/Requests/MyRequests.aspx"
            Accent="Orange" />

        <tc:DashboardCard ID="cardBookings" runat="server"
            Title="My Bookings"
            Description="View your scheduled sessions"
            NavigateUrl="~/Student/Bookings/MyBookings.aspx"
            Accent="Blue" />

        <tc:DashboardCard ID="cardReviews" runat="server"
            Title="Reviews"
            Description="Rate and review your tutors"
            NavigateUrl="~/Student/Reviews/MyReviews.aspx"
            Accent="Gold" />

    </div>

</asp:Content>
