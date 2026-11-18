<%@ Page Title="My Profile" Language="VB" MasterPageFile="~/MasterPages/Tutor.Master"
    AutoEventWireup="false" CodeBehind="MyProfile.aspx.vb"
    Inherits="TutorConnect.TutorArea.MyProfile" Async="true" %>

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
            <div class="form-row">
                <asp:Label ID="lblLocation" runat="server" AssociatedControlID="txtLocation">Location</asp:Label>
                <asp:TextBox ID="txtLocation" runat="server" MaxLength="100" />
            </div>
            <div class="form-row">
                <asp:Label ID="lblQualification" runat="server" AssociatedControlID="txtQualification">Qualification</asp:Label>
                <asp:TextBox ID="txtQualification" runat="server" MaxLength="100" />
            </div>
            <div class="form-row">
                <asp:Label ID="lblExperience" runat="server" AssociatedControlID="txtExperienceYears">Years of experience</asp:Label>
                <asp:TextBox ID="txtExperienceYears" runat="server" TextMode="Number" MaxLength="2" />
            </div>
            <div class="form-row">
                <asp:Label ID="lblRate" runat="server" AssociatedControlID="txtRate">Rate per session (R)</asp:Label>
                <asp:TextBox ID="txtRate" runat="server" TextMode="Number" MaxLength="8" />
            </div>
            <%-- Approval is the administrator's decision, so it is shown, never edited. --%>
            <div class="form-row">
                <span class="field-legend">Approval status</span>
                <p class="form-row__hint"><asp:Literal ID="litApproval" runat="server" Mode="Encode" /></p>
            </div>
        </div>

        <div class="form-row">
            <asp:Label ID="lblBio" runat="server" AssociatedControlID="txtBio">About you</asp:Label>
            <asp:TextBox ID="txtBio" runat="server" TextMode="MultiLine" MaxLength="1000"
                         placeholder="This is shown to students on your profile." />
            <p class="form-row__hint">1000 characters maximum.</p>
        </div>

        <div class="form-actions">
            <asp:Button ID="btnSave" runat="server" Text="Save" CssClass="btn btn--primary" />
            <asp:HyperLink ID="lnkCancel" runat="server" NavigateUrl="~/Tutor/Dashboard.aspx"
                           CssClass="btn btn--ghost">Cancel</asp:HyperLink>
        </div>
    </div>

</asp:Content>
