<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Error500.aspx.vb"
    Inherits="TutorConnect.ErrorPages.Error500" %>

<!DOCTYPE html>
<html lang="en-ZA">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Something went wrong - TutorConnect</title>
    <link rel="icon" href="<%: ResolveUrl("~/Images/favicon.png") %>" type="image/png" />
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/themes/colors.css") %>" />
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/themes/typography.css") %>" />
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/site.css") %>" />
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/errors.css") %>" />
</head>
<body class="app-page error-page">
    <main class="error-panel">
        <p class="error-panel__code">500</p>
        <h1 class="error-panel__title">Something went wrong</h1>
        <p class="error-panel__text">
            The problem has been recorded. If you need to report it, quote the reference below.
        </p>

        <%-- A reference only. No exception message, no stack trace: those go to the log,
             never to the browser (SEC-11). --%>
        <p class="error-panel__reference">
            Reference: <asp:Literal ID="litReference" runat="server" Mode="Encode" />
        </p>

        <div class="error-panel__actions">
            <asp:HyperLink ID="lnkLogin" runat="server" NavigateUrl="~/Account/Login.aspx"
                           CssClass="btn btn--primary">Back to login</asp:HyperLink>
        </div>
    </main>
</body>
</html>
