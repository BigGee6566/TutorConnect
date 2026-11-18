<%@ Page Title="Login" Language="VB" MasterPageFile="~/MasterPages/Auth.Master"
    AutoEventWireup="false" CodeBehind="Login.aspx.vb" Inherits="TutorConnect.Account.Login"
    Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="LoginTitle" ContentPlaceHolderID="TitleContent" runat="server">Login - TutorConnect</asp:Content>

<%-- Copy for the photograph panel. --%>
<asp:Content ID="LoginBrand" ContentPlaceHolderID="BrandContent" runat="server">

    <p class="auth-brand__badge">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2" aria-hidden="true" focusable="false">
            <path d="M3 21h18" />
            <path d="M5 21V10l7-5 7 5v11" />
            <path d="M10 21v-5h4v5" />
        </svg>
        Academic Tutoring &amp; Governance Portal
    </p>

    <div class="auth-brand__lower">
        <h2 class="auth-brand__heading">Connect. Learn.<br />Succeed.</h2>
        <p class="auth-brand__text">
            Streamlining higher education tutoring with verified peer mentors, responsive
            schedule booking, and comprehensive institutional administrative oversight.
        </p>
    </div>

</asp:Content>

<asp:Content ID="LoginBody" ContentPlaceHolderID="MainContent" runat="server">

    <div class="auth-form">

        <%-- The artwork already contains the wordmark and the tagline, so neither is
             repeated as text underneath it. width/height are stated so the browser
             reserves the space and the heading does not jump as the image decodes. --%>
        <img class="auth-form__logo"
             src='<%: ResolveUrl("~/Images/branding/tutorconnect-logo.png") %>'
             alt="TutorConnect" width="180" height="130" />

        <h1 class="auth-form__title">Welcome Back!</h1>
        <p class="auth-form__subtitle">Login to your account</p>

        <%-- Confirmation shown after registering. --%>
        <tc:AlertMessage ID="alertRegistered" runat="server" />

        <%-- Error region sits above the form and announces itself (F.6). --%>
        <asp:Panel ID="pnlError" runat="server" CssClass="alert alert--error"
                   role="alert" Visible="false">
            <asp:Literal ID="litError" runat="server" Mode="Encode" />
        </asp:Panel>

        <div class="field field--with-icon">
            <asp:Label ID="lblUserName" runat="server" AssociatedControlID="txtUserName">Username</asp:Label>
            <div class="field__control">
                <svg class="field__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="2" aria-hidden="true" focusable="false">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                    <circle cx="12" cy="7" r="4" />
                </svg>
                <asp:TextBox ID="txtUserName" runat="server" placeholder="Enter your username"
                             autocomplete="username" MaxLength="100" />
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
                             placeholder="Enter your password" autocomplete="current-password" />

                <%-- type="button" so it can never submit the form. --%>
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

        <asp:Button ID="btnLogin" runat="server" Text="Login" CssClass="btn btn--primary btn--block" />

        <asp:HyperLink ID="lnkRegister" runat="server" NavigateUrl="~/Account/Register.aspx"
                       CssClass="btn btn--outline btn--block">Register New Account</asp:HyperLink>

        <asp:HyperLink ID="lnkForgot" runat="server" NavigateUrl="~/Account/ForgotPassword.aspx"
                       CssClass="auth-form__link">Forgot password?</asp:HyperLink>

    </div>

</asp:Content>

<asp:Content ID="LoginScripts" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="<%: ResolveUrl("~/Scripts/password-toggle.js") %>"></script>
</asp:Content>
