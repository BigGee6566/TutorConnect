<%@ Page Title="Register" Language="VB" MasterPageFile="~/MasterPages/Auth.Master"
    AutoEventWireup="false" CodeBehind="Register.aspx.vb" Inherits="TutorConnect.Account.Register"
    Async="true" %>

<asp:Content ID="RegisterTitle" ContentPlaceHolderID="TitleContent" runat="server">Register - TutorConnect</asp:Content>

<%-- Copy for the photograph panel. --%>
<asp:Content ID="RegisterBrand" ContentPlaceHolderID="BrandContent" runat="server">

    <p class="auth-brand__badge">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2" aria-hidden="true" focusable="false">
            <path d="M22 10 12 5 2 10l10 5 10-5z" />
            <path d="M6 12v5c0 1.1 2.7 2 6 2s6-.9 6-2v-5" />
        </svg>
        Join the TutorConnect Network
    </p>

    <div class="auth-brand__lower">
        <h2 class="auth-brand__heading">Connect. Learn.<br />Succeed.</h2>
        <p class="auth-brand__text">
            Whether you want to master demanding coursework or share your academic
            expertise as an accredited peer tutor, TutorConnect is your campus hub.
        </p>
    </div>

</asp:Content>

<asp:Content ID="RegisterBody" ContentPlaceHolderID="MainContent" runat="server">

    <div class="auth-form auth-form--wide">

        <%-- The artwork already contains the wordmark and the tagline, so neither is
             repeated as text underneath it. --%>
        <img class="auth-form__logo"
             src='<%: ResolveUrl("~/Images/branding/tutorconnect-logo.png") %>'
             alt="TutorConnect" width="180" height="130" />

        <h1 class="auth-form__title">Create your account</h1>
        <p class="auth-form__subtitle">Join TutorConnect as a student or a tutor</p>

        <asp:Panel ID="pnlError" runat="server" CssClass="alert alert--error" role="alert" Visible="false">
            <asp:Literal ID="litError" runat="server" Mode="Encode" />
        </asp:Panel>

        <fieldset class="account-type-fieldset">
            <legend class="field-legend">Account type</legend>
            <div class="account-type">
                <div class="account-type__option">
                    <asp:RadioButton ID="rdoStudent" runat="server" GroupName="AccountType"
                                     Text="Student" AutoPostBack="true" Checked="true" />
                </div>
                <div class="account-type__option">
                    <asp:RadioButton ID="rdoTutor" runat="server" GroupName="AccountType"
                                     Text="Tutor" AutoPostBack="true" />
                </div>
            </div>
        </fieldset>

        <div class="field-group">
            <div class="field">
                <asp:Label ID="lblFirstName" runat="server" AssociatedControlID="txtFirstName">First name</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtFirstName" runat="server" MaxLength="50" autocomplete="given-name" />
                </div>
            </div>
            <div class="field">
                <asp:Label ID="lblLastName" runat="server" AssociatedControlID="txtLastName">Last name</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtLastName" runat="server" MaxLength="50" autocomplete="family-name" />
                </div>
            </div>
        </div>

        <div class="field-group">
            <div class="field">
                <asp:Label ID="lblEmail" runat="server" AssociatedControlID="txtEmail">Email address</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtEmail" runat="server" TextMode="Email" MaxLength="100" autocomplete="email" />
                </div>
            </div>
            <div class="field">
                <asp:Label ID="lblUserName" runat="server" AssociatedControlID="txtUserName">Username</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtUserName" runat="server" MaxLength="50" autocomplete="username" />
                </div>
            </div>
        </div>

        <div class="field-group">
            <div class="field">
                <asp:Label ID="lblCell" runat="server" AssociatedControlID="txtCellNumber">Cell number</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtCellNumber" runat="server" TextMode="Phone" MaxLength="15"
                                 placeholder="0821234567" autocomplete="tel" />
                </div>
            </div>
            <div class="field">
                <asp:Label ID="lblGender" runat="server" AssociatedControlID="ddlGender">Gender</asp:Label>
                <div class="field__control">
                    <asp:DropDownList ID="ddlGender" runat="server">
                        <asp:ListItem Value="">Select...</asp:ListItem>
                        <asp:ListItem Value="Female">Female</asp:ListItem>
                        <asp:ListItem Value="Male">Male</asp:ListItem>
                        <asp:ListItem Value="Other">Other</asp:ListItem>
                    </asp:DropDownList>
                </div>
            </div>
        </div>

        <%-- "Location" is the canonical name for this field. Never "Address". --%>
        <div class="field">
            <asp:Label ID="lblLocation" runat="server" AssociatedControlID="txtLocation">Location</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtLocation" runat="server" MaxLength="100"
                             placeholder="For example East London" />
            </div>
        </div>

        <%-- Student-only field. Toggled server-side on postback, so it works without JavaScript. --%>
        <asp:Panel ID="pnlStudentFields" runat="server" CssClass="role-fields">
            <div class="field">
                <asp:Label ID="lblStudentNo" runat="server" AssociatedControlID="txtStudentNo">Student number</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtStudentNo" runat="server" MaxLength="20" placeholder="STU2026001" />
                </div>
            </div>
        </asp:Panel>

        <%-- Tutor-only fields. --%>
        <asp:Panel ID="pnlTutorFields" runat="server" CssClass="role-fields" Visible="false">
            <div class="field">
                <asp:Label ID="lblQualification" runat="server" AssociatedControlID="txtQualification">Qualification</asp:Label>
                <div class="field__control">
                    <asp:TextBox ID="txtQualification" runat="server" MaxLength="100"
                                 placeholder="BSc Computer Science" />
                </div>
            </div>
            <div class="field-group">
                <div class="field">
                    <asp:Label ID="lblExperience" runat="server" AssociatedControlID="txtExperienceYears">Years of experience</asp:Label>
                    <div class="field__control">
                        <asp:TextBox ID="txtExperienceYears" runat="server" TextMode="Number" MaxLength="2" />
                    </div>
                </div>
                <div class="field">
                    <asp:Label ID="lblRate" runat="server" AssociatedControlID="txtRatePerSession">Rate per session (R)</asp:Label>
                    <div class="field__control">
                        <asp:TextBox ID="txtRatePerSession" runat="server" TextMode="Number" MaxLength="8" />
                    </div>
                </div>
            </div>
        </asp:Panel>

        <div class="field field--with-toggle">
            <asp:Label ID="lblPassword" runat="server" AssociatedControlID="txtPassword">Password</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtPassword" runat="server" TextMode="Password"
                             autocomplete="new-password" data-strength-source="true" />
                <button type="button" class="password-toggle" data-password-toggle="txtPassword"
                        aria-pressed="false" aria-label="Show password">
                    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor"
                         stroke-width="2" aria-hidden="true" focusable="false">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                        <circle cx="12" cy="12" r="3" />
                    </svg>
                </button>
            </div>
            <p class="field__hint" id="passwordRules">At least 8 characters, including one letter and one number.</p>
            <div class="strength" data-strength-meter hidden>
                <div class="strength__bar"><div class="strength__fill" data-strength-fill></div></div>
                <p class="strength__label" data-strength-label aria-live="polite"></p>
            </div>
        </div>

        <div class="field">
            <asp:Label ID="lblConfirm" runat="server" AssociatedControlID="txtConfirmPassword">Confirm password</asp:Label>
            <div class="field__control">
                <asp:TextBox ID="txtConfirmPassword" runat="server" TextMode="Password"
                             autocomplete="new-password" data-strength-confirm="true" />
            </div>
            <p class="strength__label" data-match-label aria-live="polite"></p>
        </div>

        <asp:Button ID="btnRegister" runat="server" Text="Create Account" CssClass="btn btn--primary btn--block" />

        <asp:HyperLink ID="lnkLogin" runat="server" NavigateUrl="~/Account/Login.aspx"
                       CssClass="auth-form__link">Already have an account? Login</asp:HyperLink>

    </div>

</asp:Content>

<asp:Content ID="RegisterScripts" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="<%: ResolveUrl("~/Scripts/password-toggle.js") %>"></script>
    <script src="<%: ResolveUrl("~/Scripts/auth.js") %>"></script>
</asp:Content>
