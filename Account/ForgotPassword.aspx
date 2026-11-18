<%@ Page Title="Forgot password" Language="VB" MasterPageFile="~/MasterPages/Auth.Master"
    AutoEventWireup="false" CodeBehind="ForgotPassword.aspx.vb" Inherits="TutorConnect.Account.ForgotPassword" %>

<asp:Content ID="ForgotTitle" ContentPlaceHolderID="TitleContent" runat="server">Forgot password - TutorConnect</asp:Content>

<asp:Content ID="ForgotBody" ContentPlaceHolderID="MainContent" runat="server">

    <div class="auth-form">

        <h1 class="auth-form__title">Forgot your password?</h1>
        <p class="auth-form__subtitle">How to regain access to your account</p>

        <%-- Email and SMS delivery are out of scope for this project (Part N), so there is
             no self-service reset link. Rather than show a form that silently does
             nothing, this page states the real process. --%>
        <div class="alert alert--info" role="status">
            <p>
                TutorConnect does not send password reset emails. Notifications in this
                system are in-app only.
            </p>
            <p>
                To have your password reset, contact the system administrator with your
                username and student or staff number. An administrator can reset it for
                you; nobody can read your existing password, because only a one-way hash
                of it is stored.
            </p>
        </div>

        <asp:HyperLink ID="lnkBack" runat="server" NavigateUrl="~/Account/Login.aspx"
                       CssClass="btn btn--outline btn--block">Back to login</asp:HyperLink>

    </div>

</asp:Content>
