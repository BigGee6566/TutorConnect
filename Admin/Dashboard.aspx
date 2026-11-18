<%@ Page Title="Dashboard" Language="VB" MasterPageFile="~/MasterPages/Admin.Master"
    AutoEventWireup="false" CodeBehind="Dashboard.aspx.vb"
    Inherits="TutorConnect.AdminArea.Dashboard" Async="true" %>

<asp:Content ID="DashTitle" ContentPlaceHolderID="TitleContent" runat="server">Dashboard - TutorConnect</asp:Content>

<asp:Content ID="DashBody" ContentPlaceHolderID="MainContent" runat="server">

    <%-- Each tile now opens the list behind its number. They were plain blocks in Phase 4
         only because those screens did not exist yet. --%>
    <div class="stat-grid">

        <a class="stat-card" href="<%: ResolveUrl("~/Admin/Users/ManageUsers.aspx?role=Student") %>">
            <div class="stat-card__value"><asp:Literal ID="litStudents" runat="server" Mode="Encode" /></div>
            <div class="stat-card__label">Total students</div>
        </a>

        <a class="stat-card" href="<%: ResolveUrl("~/Admin/Users/ManageUsers.aspx?role=Tutor") %>">
            <div class="stat-card__value"><asp:Literal ID="litTutors" runat="server" Mode="Encode" /></div>
            <div class="stat-card__label">Total tutors</div>
        </a>

        <a class="stat-card" href="<%: ResolveUrl("~/Admin/Users/ManageUsers.aspx?role=Tutor&pending=1") %>">
            <div class="stat-card__value"><asp:Literal ID="litAwaiting" runat="server" Mode="Encode" /></div>
            <div class="stat-card__label">Tutors awaiting approval</div>
        </a>

        <a class="stat-card" href="<%: ResolveUrl("~/Admin/Bookings/ManageBookings.aspx") %>">
            <div class="stat-card__value"><asp:Literal ID="litRequestsToday" runat="server" Mode="Encode" /></div>
            <div class="stat-card__label">Requests today</div>
        </a>

        <a class="stat-card" href="<%: ResolveUrl("~/Admin/Bookings/ManageBookings.aspx") %>">
            <div class="stat-card__value"><asp:Literal ID="litBookingsWeek" runat="server" Mode="Encode" /></div>
            <div class="stat-card__label">Bookings this week</div>
        </a>

        <a class="stat-card" href="<%: ResolveUrl("~/Admin/Reviews/ManageReviews.aspx") %>">
            <div class="stat-card__value"><asp:Literal ID="litAverageRating" runat="server" Mode="Encode" /></div>
            <div class="stat-card__label">Average platform rating</div>
        </a>

    </div>

</asp:Content>
