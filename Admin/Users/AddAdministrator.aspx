<%@ Page Title="Add Administrator" Language="VB" MasterPageFile="~/MasterPages/Admin.Master"
    AutoEventWireup="false" CodeBehind="AddAdministrator.aspx.vb"
    Inherits="TutorConnect.AdminArea.AddAdministrator" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Add Administrator - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">New administrator account</h2>
        </div>

        <div class="panel__body">

            <p class="form-row__hint">
                An administrator can approve tutors, deactivate accounts, moderate reviews and
                create further administrators. Only create one for somebody who needs all of
                that.
            </p>

            <div class="field">
                <asp:Label ID="lblUserName" runat="server" AssociatedControlID="txtUserName">Username</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtUserName" runat="server" MaxLength="50"
                                 autocomplete="off" placeholder="Choose a username" />
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblFirstName" runat="server" AssociatedControlID="txtFirstName">First name</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtFirstName" runat="server" MaxLength="50" placeholder="First name" />
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblLastName" runat="server" AssociatedControlID="txtLastName">Last name</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtLastName" runat="server" MaxLength="50" placeholder="Last name" />
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblGender" runat="server" AssociatedControlID="ddlGender">Gender</asp:Label>
                <div class="field__control">
                    <asp:DropDownList ID="ddlGender" runat="server">
                        <asp:ListItem Text="Female" Value="Female" />
                        <asp:ListItem Text="Male" Value="Male" />
                        <asp:ListItem Text="Other" Value="Other" Selected="True" />
                    </asp:DropDownList>
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblEmail" runat="server" AssociatedControlID="txtEmail">Email address</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtEmail" runat="server" TextMode="Email" MaxLength="100"
                                 autocomplete="off" placeholder="name@example.com" />
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblCell" runat="server" AssociatedControlID="txtCell">Cell number</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtCell" runat="server" MaxLength="15"
                                 autocomplete="off" placeholder="0821234567" />
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblLocation" runat="server" AssociatedControlID="txtLocation">Location</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtLocation" runat="server" MaxLength="100" placeholder="East London" />
                </div>
            </div>

            <div class="field field--with-toggle">
                <asp:Label ID="lblPassword" runat="server" AssociatedControlID="txtPassword">Temporary password</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtPassword" runat="server" TextMode="Password"
                                 autocomplete="new-password" placeholder="Choose a password" />

                    <button type="button" class="password-toggle" data-password-toggle="txtPassword"
                            aria-pressed="false" aria-label="Show password">
                        <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor"
                             stroke-width="2" aria-hidden="true" focusable="false">
                            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                            <circle cx="12" cy="12" r="3" />
                        </svg>
                    </button>
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblConfirm" runat="server" AssociatedControlID="txtConfirm">Confirm password</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtConfirm" runat="server" TextMode="Password"
                                 autocomplete="new-password" placeholder="Repeat the password" />
                </div>
            </div>

            <p class="form-row__hint">
                You will need to tell the new administrator this password yourself. It is
                stored only as a one-way hash, so it cannot be read back from any screen -
                including this one.
            </p>

            <div class="form-actions">
                <asp:Button ID="btnCreate" runat="server" Text="Create administrator"
                            CssClass="btn btn--primary" />
                <asp:HyperLink ID="lnkCancel" runat="server" CssClass="btn btn--outline"
                               NavigateUrl="~/Admin/Users/ManageUsers.aspx">Cancel</asp:HyperLink>
            </div>

        </div>
    </div>

</asp:Content>

<asp:Content ID="S" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="<%: ResolveUrl("~/Scripts/password-toggle.js") %>"></script>
</asp:Content>
