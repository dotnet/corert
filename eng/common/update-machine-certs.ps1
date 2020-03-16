# This seems to update the machine cert store so that python can download the files as required by emscripten's install
$WebsiteURL="storage.googleapis.com"
#$WebsiteURL="https://storage.googleapis.com/webassembly/emscripten-releases-builds/deps/node-v12.9.1-win-x64.zip"
Try {
    $Conn = New-Object System.Net.Sockets.TcpClient($WebsiteURL,443) 
  
    Try {
        $Stream = New-Object System.Net.Security.SslStream($Conn.GetStream())
        $Stream.AuthenticateAsClient($WebsiteURL) 
   
        $Cert = $Stream.Get_RemoteCertificate()
 
        $ValidTo = [datetime]::Parse($Cert.GetExpirationDatestring())
   
        Write-Host "`nConnection Successfull" -ForegroundColor DarkGreen
        Write-Host "Website: $WebsiteURL"
    }
    Catch { Throw $_ }
    Finally { $Conn.close() }
    }
    Catch {
            Write-Host "`nError occurred connecting to $($WebsiteURL)" -ForegroundColor Yellow
            Write-Host "Website: $WebsiteURL"
            Write-Host "Status:" $_.exception.innerexception.message -ForegroundColor Yellow
            Write-Host ""
}
