<%@ Page Title="First-run setup" Language="VB" MasterPageFile="~/MasterPages/Auth.Master"
    AutoEventWireup="false" CodeBehind="Setup.aspx.vb" Inherits="TutorConnect.Account.Setup"
    Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="SetupTitle" ContentPlaceHolderID="TitleContent" runat="server">First-run setup - TutorConnect</asp:Content>

<asp:Content ID="SetupBody" ContentPlaceHolderID="MainContent" runat="server">

    <div class="auth-form">

        <h1 class="auth-form__title">First-run setup</h1>
        <p class="auth-form__subtitle">Create the administrator account for this installation</p>

        <asp:Panel ID="pnlError" runat="server" CssClass="alert alert--error"
                   role="alert" Visible="false">
            <asp:Literal ID="litError" runat="server" Mode="Encode" />
        </asp:Panel>

        <%-- Shown instead of the form once the system already has an administrator. --%>
        <asp:Panel ID="pnlClosed" runat="server" Visible="false">
            <tc:AlertMessage ID="alertClosed" runat="server" />
            <asp:HyperLink ID="lnkSignIn" runat="server" NavigateUrl="~/Account/Login.aspx"
                           CssClass="btn btn--primary btn--block">Go to Login</asp:HyperLink>
        </asp:Panel>

        <asp:Panel ID="pnlForm" runat="server">

            <p class="auth-form__subtitle">
                This page works only while no administrator exists. It closes permanently as
                soon as this account is created.
            </p>

            <div class="field field--with-icon">
                <asp:Label ID="lblUserName" runat="server" AssociatedControlID="txtUserName">Username</asp:Label>
                <div class="field__control">
                    <svg class="field__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" aria-hidden="true" focusable="false">
                        <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                        <circle cx="12" cy="7" r="4" />
                    </svg>
                    <asp:TextBox ID="txtUserName" runat="server" placeholder="Choose a username"
                                 autocomplete="username" MaxLength="50" />
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblFirstName" runat="server" AssociatedControlID="txtFirstName">First name</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtFirstName" runat="server" placeholder="First name" MaxLength="50" />
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblLastName" runat="server" AssociatedControlID="txtLastName">Last name</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtLastName" runat="server" placeholder="Last name" MaxLength="50" />
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

            <div class="field field--with-icon">
                <asp:Label ID="lblEmail" runat="server" AssociatedControlID="txtEmail">Email address</asp:Label>
                <div class="field__control">
                    <svg class="field__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" aria-hidden="true" focusable="false">
                        <rect x="2" y="4" width="20" height="16" rx="2" />
                        <path d="m2 7 10 6 10-6" />
                    </svg>
                    <asp:TextBox ID="txtEmail" runat="server" TextMode="Email"
                                 placeholder="you@example.com" autocomplete="email" MaxLength="100" />
                </div>
            </div>

            <div class="field field--with-icon">
                <asp:Label ID="lblCell" runat="server" AssociatedControlID="txtCell">Cell number</asp:Label>
                <div class="field__control">
                    <svg class="field__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" aria-hidden="true" focusable="false">
                        <rect x="6" y="2" width="12" height="20" rx="2" />
                        <path d="M11 18h2" />
                    </svg>
                    <asp:TextBox ID="txtCell" runat="server" placeholder="0821234567"
                                 autocomplete="tel" MaxLength="15" />
                </div>
            </div>

            <div class="field">
                <asp:Label ID="lblLocation" runat="server" AssociatedControlID="txtLocation">Location</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtLocation" runat="server" placeholder="East London" MaxLength="100" />
                </div>
            </div>

            <div class="field field--with-icon field--with-toggle">
                <asp:Label ID="lblPassword" runat="server" AssociatedControlID="txtPassword">Password</asp:Label>
                <div class="field__control">
                    <svg class="field__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" aria-hidden="true" focusable="false">
                        <rect x="3" y="11" width="18" height="11" rx="2" />
                        <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                    </svg>
                    <asp:TextBox ID="txtPassword" runat="server" TextMode="Password"
                                 placeholder="Choose a password" autocomplete="new-password" />

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

            <div class="field field--with-icon">
                <asp:Label ID="lblConfirm" runat="server" AssociatedControlID="txtConfirm">Confirm password</asp:Label>
                <div class="field__control">
                    <svg class="field__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" aria-hidden="true" focusable="false">
                        <rect x="3" y="11" width="18" height="11" rx="2" />
                        <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                    </svg>
                    <asp:TextBox ID="txtConfirm" runat="server" TextMode="Password"
                                 placeholder="Repeat the password" autocomplete="new-password" />
                </div>
            </div>

            <asp:Button ID="btnCreate" runat="server" Text="Create administrator"
                        CssClass="btn btn--primary btn--block" />

        </asp:Panel>

    </div>

</asp:Content>

<asp:Content ID="SetupScripts" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="<%: ResolveUrl("~/Scripts/password-toggle.js") %>"></script>
</asp:Content>
