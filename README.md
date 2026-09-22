# 거상 통합 도우미

Windows 10/11용 개인 도우미입니다. C# .NET 8과 WPF로 만들었으며, 일반 인터넷 탭, 거타 육의전 원본 페이지, 주막 임무 타이머, 체크리스트, CPU 온도, 계산기, 사용자 이미지 버튼을 한 창에서 사용합니다.

## 다른 PC에서 수정하기

1. Windows PC에 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)와 Git을 설치합니다. Visual Studio 2022를 쓴다면 **.NET 데스크톱 개발** 구성 요소를 선택합니다.
2. 이 저장소를 복제하거나 ZIP으로 다운로드합니다.
3. `GeosangHub.csproj`를 Visual Studio로 열거나, 이 폴더에서 `dotnet restore`와 `dotnet build -c Release`를 실행합니다.
4. 코드를 수정한 뒤 `dotnet run --project GeosangHub.csproj`로 실행합니다.

웹 화면을 표시하려면 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)이 필요합니다. 빌드 시 `Microsoft.Web.WebView2` 패키지는 NuGet에서 복원됩니다.

CPU 온도는 [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)로 읽습니다. 빌드 시 NuGet에서 함께 복원됩니다. 센서가 제공되지 않는 PC에서는 온도 대신 안내 문구가 나오며, 일부 센서는 관리자 권한이 필요합니다. 라이브러리는 [MPL 2.0](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE) 라이선스를 따릅니다.

## 배포용 파일 만들기

프로젝트 폴더에서 다음 명령을 실행합니다.

```powershell
dotnet publish .\GeosangHub.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\publish
```

생성된 `publish` 폴더 **전체**를 다른 PC로 복사합니다. 실행 파일만 단독으로 복사하면 실행되지 않을 수 있습니다.

## 저장 데이터

- 통합 창 배치, 인터넷 탭·북마크: `%LOCALAPPDATA%\GeosangIntegratedHub\layout.json`
- 주막 임무, 체크리스트, 이미지 버튼 설정: `%LOCALAPPDATA%\GeosangHelper\state.json`
- 등록한 이미지의 사본: `%LOCALAPPDATA%\GeosangHelper\shortcut-images`

이 데이터는 사용자의 PC에 별도로 저장되며 저장소에 포함되지 않습니다. 기존 PC의 설정도 옮기고 싶다면 앱을 종료한 뒤 해당 폴더들을 새 PC의 같은 위치로 복사하세요.

## 프로젝트 파일

- `MainWindow.xaml` / `.xaml.cs`: 통합 화면과 브라우저 탭
- `GeosangHelperPane.xaml` / `.xaml.cs`: 주막 타이머, 체크리스트, 환경설정
- `CalculatorPane.xaml` / `.xaml.cs`: 계산기
- `CpuTemperaturePane.xaml` / `.xaml.cs`: CPU 온도 센서 표시
- `ImageShortcutDialog.cs` / `ImagePreviewWindow.cs`: 이미지 버튼 등록과 보기
- `HubSettings.cs`, `Models.cs`, `Storage.cs`: 로컬 설정 저장

빌드 결과물(`bin`, `obj`, `publish`)과 개인 설정 파일은 Git에서 제외됩니다.
