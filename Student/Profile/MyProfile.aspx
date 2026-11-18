<%@ Page Title="My Profile" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="MyProfile.aspx.vb"
    Inherits="TutorConnect.StudentArea.MyProfile" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">My Profile - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="form-card">
        <div class="form-grid">

            <div class="form-row">
                <asp:Label ID="lblFirstName" runat="server" AssociatedControlID="txtFirstName">First name</asp:Label>
                <asp:TextBox ID="txtFirstName" runat="server" MaxLength="50" />
            </div>

            <div class="form-row">
                <asp:Label ID="lblLastName" runat="server" AssociatedControlID="txtLastName">Last name</asp:Label>
                <asp:TextBox ID="txtLastName" runat="server" MaxLength="50" />
            </div>

            <div class="form-row">
                <asp:Label ID="lblGender" runat="server" AssociatedControlID="ddlGender">Gender</asp:Label>
                <asp:DropDownList ID="ddlGender" runat="server">
                    <asp:ListItem Value="">Select...</asp:ListItem>
                    <asp:ListItem Value="Female">Female</asp:ListItem>
                    <asp:ListItem Value="Male">Male</asp:ListItem>
                    <asp:ListItem Value="Other">Other</asp:ListItem>
                </asp:DropDownList>
            </div>

            <div class="form-row">
                <asp:Label ID="lblEmail" runat="server" AssociatedControlID="txtEmail">Email address</asp:Label>
                <asp:TextBox ID="txtEmail" runat="server" TextMode="Email" MaxLength="100" />
            </div>

            <div class="form-row">
                <asp:Label ID="lblCell" runat="server" AssociatedControlID="txtCellNumber">Cell number</asp:Label>
                <asp:TextBox ID="txtCellNumber" runat="server" TextMode="Phone" MaxLength="15" />
            </div>

            <%-- "Location" is canonical everywhere: ERD, column, parameter, label. --%>
            <div class="form-row">
                <asp:Label ID="lblLocation" runat="server" AssociatedControlID="txtLocation">Location</asp:Label>
                <asp:TextBox ID="txtLocation" runat="server" MaxLength="100" />
            </div>

            <%-- Read-only: a student number and username identify the account and are not
                 the student's to change. --%>
            <div class="form-row">
                <asp:Label ID="lblStudentNo" runat="server" AssociatedControlID="txtStudentNo">Student number</asp:Label>
                <asp:TextBox ID="txtStudentNo" runat="server" ReadOnly="true" />
            </div>

            <div class="form-row">
                <asp:Label ID="lblUserName" runat="server" AssociatedControlID="txtUserName">Username</asp:Label>
                <asp:TextBox ID="txtUserName" runat="server" ReadOnly="true" />
            </div>

        </div>

        <div class="form-actions">
            <asp:Button ID="btnSave" runat="server" Text="Save" CssClass="btn btn--primary" />
            <asp:HyperLink ID="lnkCancel" runat="server" NavigateUrl="~/Student/Dashboard.aspx"
                           CssClass="btn btn--ghost">Cancel</asp:HyperLink>
        </div>
    </div>

</asp:Content>
