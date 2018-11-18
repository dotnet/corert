extern "C" {
    void call_once(void(*f)(void))
    {
        f();
    }
}
